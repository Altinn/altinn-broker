using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace Altinn.Broker.Tests.LargeFile;

/// <summary>
/// Reference implementation for uploading files to Altinn Broker via TUS.
/// Supports single-stream uploads and the TUS concatenation extension for parallel partial uploads.
/// </summary>
public static class TusUploader
{
    private const string TusVersion = "1.0.0";
    private const string TusUploadPath = "/broker/api/v1/filetransfer/upload/tus";
    private const string PartialPathSegment = "partial";
    private const int MaxPatchAttempts = 5;

    public static async Task UploadAsync(
        HttpClient httpClient,
        string baseUrl,
        string fileTransferId,
        Stream source,
        long uploadSize,
        int chunkSize,
        CancellationToken cancellationToken = default)
    {
        var tusEndpoint = BuildTusEndpointUri(baseUrl, fileTransferId);
        await EnsureServerSupportsTus(httpClient, tusEndpoint, cancellationToken);

        var uploadUri = await CreateUploadAsync(httpClient, tusEndpoint, uploadSize, cancellationToken);
        var offset = await GetUploadOffsetAsync(httpClient, uploadUri, cancellationToken);
        if (offset > 0)
        {
            Console.WriteLine($"Resuming upload at offset {offset:N0}");
            source.Seek(offset, SeekOrigin.Begin);
        }

        var progress = new UploadProgress(uploadSize);
        using var progressTimer = new Timer(_ => progress.LogProgress(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var totalStopwatch = Stopwatch.StartNew();

        var buffer = new byte[chunkSize];
        while (offset < uploadSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesToRead = (int)Math.Min(chunkSize, uploadSize - offset);
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);
            if (bytesRead == 0)
            {
                throw new InvalidOperationException($"Source stream ended at offset {offset}.");
            }

            offset = await PatchChunkAsync(httpClient, uploadUri, offset, buffer, bytesRead, cancellationToken);
            progress.Update(offset);
        }

        LogCompletion("TUS upload", fileTransferId, uploadSize, totalStopwatch.Elapsed);
    }

    public static async Task UploadWithConcatenationAsync(
        HttpClient httpClient,
        string baseUrl,
        string fileTransferId,
        Stream source,
        long uploadSize,
        int chunkSize,
        int parallelPartialUploads,
        CancellationToken cancellationToken = default)
    {
        if (!source.CanSeek)
        {
            throw new InvalidOperationException("Concatenation upload requires a seekable source stream.");
        }

        var tusEndpoint = BuildTusEndpointUri(baseUrl, fileTransferId);
        await EnsureServerSupportsConcatenation(httpClient, tusEndpoint, cancellationToken);

        var partRanges = BuildPartRanges(uploadSize, parallelPartialUploads);
        var partialUris = new Uri[partRanges.Count];
        for (var i = 0; i < partRanges.Count; i++)
        {
            partialUris[i] = await CreatePartialUploadAsync(httpClient, tusEndpoint, partRanges[i].Length, cancellationToken);
        }

        var readLock = new object();
        var progress = new UploadProgress(uploadSize);
        using var progressTimer = new Timer(_ => progress.LogProgress(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var totalStopwatch = Stopwatch.StartNew();
        long progressBytes = 0;

        await Parallel.ForAsync(0, partRanges.Count, cancellationToken, async (partIndex, ct) =>
        {
            var part = partRanges[partIndex];
            await UploadPartialAsync(
                httpClient,
                partialUris[partIndex],
                source,
                readLock,
                part.StartOffset,
                part.Length,
                chunkSize,
                uploadedBytes => progress.Update(Interlocked.Add(ref progressBytes, uploadedBytes)),
                ct);
        });

        await CreateFinalUploadAsync(httpClient, tusEndpoint, partialUris, cancellationToken);
        LogCompletion("TUS concatenation upload", fileTransferId, uploadSize, totalStopwatch.Elapsed);
    }

    private sealed class UploadProgress(long totalSize)
    {
        private long _offset;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public void Update(long offset) => Interlocked.Exchange(ref _offset, offset);

        public void LogProgress()
        {
            var currentOffset = Interlocked.Read(ref _offset);
            var elapsedSeconds = Math.Max(_stopwatch.Elapsed.TotalSeconds, 0.001);
            Console.WriteLine(
                $"Progress: {currentOffset * 100.0 / totalSize:F1}% " +
                $"({currentOffset / (1024.0 * 1024 * 1024):N2} GiB / {totalSize / (1024.0 * 1024 * 1024):N2} GiB) " +
                $"avg {currentOffset / elapsedSeconds / (1024 * 1024):N2} MiB/s");
        }
    }

    private static Uri BuildTusEndpointUri(string baseUrl, string fileTransferId)
        => new($"{baseUrl.TrimEnd('/')}{TusUploadPath}/{fileTransferId}");

    private static async Task EnsureServerSupportsTus(HttpClient httpClient, Uri tusEndpoint, CancellationToken cancellationToken)
    {
        using var response = await SendTusRequestAsync(httpClient, HttpMethod.Options, tusEndpoint, cancellationToken);
        await EnsureSuccessAsync(response, "TUS OPTIONS");
    }

    private static async Task EnsureServerSupportsConcatenation(HttpClient httpClient, Uri tusEndpoint, CancellationToken cancellationToken)
    {
        using var response = await SendTusRequestAsync(httpClient, HttpMethod.Options, tusEndpoint, cancellationToken);
        await EnsureSuccessAsync(response, "TUS OPTIONS");

        if (!response.Headers.TryGetValues("Tus-Extension", out var extensions)
            || !extensions.Any(value => value.Contains("concatenation", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Server does not advertise TUS concatenation support.");
        }
    }

    private static async Task<Uri> CreateUploadAsync(
        HttpClient httpClient,
        Uri tusEndpoint,
        long uploadSize,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, tusEndpoint);
        request.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        request.Headers.TryAddWithoutValidation("Upload-Length", uploadSize.ToString());

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            return ResolveUploadUri(tusEndpoint, response.Headers.Location)
                ?? throw new InvalidOperationException("TUS POST succeeded but no Location header was returned.");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return tusEndpoint;
        }

        await EnsureSuccessAsync(response, "TUS POST");
        throw new UnreachableException();
    }

    private static async Task<Uri> CreatePartialUploadAsync(
        HttpClient httpClient,
        Uri tusEndpoint,
        long partLength,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, tusEndpoint);
        request.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        request.Headers.TryAddWithoutValidation("Upload-Length", partLength.ToString());
        request.Headers.TryAddWithoutValidation("Upload-Concat", "partial");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "TUS partial POST", HttpStatusCode.Created);

        return ResolvePartialUploadUri(tusEndpoint, response.Headers.Location)
            ?? throw new InvalidOperationException("TUS partial POST succeeded but no Location header was returned.");
    }

    private static Uri? ResolvePartialUploadUri(Uri tusEndpoint, Uri? location)
    {
        var resolved = ResolveUploadUri(tusEndpoint, location);
        if (resolved is null)
        {
            return null;
        }

        var fileTransferId = tusEndpoint.AbsolutePath.TrimEnd('/').Split('/').Last();
        return CanonicalizePartialUploadPath(resolved, fileTransferId);
    }

    private static async Task CreateFinalUploadAsync(
        HttpClient httpClient,
        Uri tusEndpoint,
        IReadOnlyList<Uri> partialUris,
        CancellationToken cancellationToken)
    {
        foreach (var partialUri in partialUris)
        {
            await EnsurePartialUploadCompleteAsync(httpClient, partialUri, cancellationToken);
        }

        var partialReferences = string.Join(' ', partialUris.Select(ToTusConcatReference));
        using var request = new HttpRequestMessage(HttpMethod.Post, tusEndpoint);
        request.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        request.Headers.TryAddWithoutValidation("Upload-Concat", $"final;{partialReferences}");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "TUS final POST", HttpStatusCode.Created);
    }

    private static async Task EnsurePartialUploadCompleteAsync(
        HttpClient httpClient,
        Uri partialUploadUri,
        CancellationToken cancellationToken)
    {
        using var response = await SendTusRequestAsync(httpClient, HttpMethod.Head, partialUploadUri, cancellationToken);
        await EnsureSuccessAsync(response, "TUS partial HEAD", requestUri: partialUploadUri);

        if (!response.Headers.TryGetValues("Upload-Length", out var lengthValues)
            || !long.TryParse(lengthValues.FirstOrDefault(), out var uploadLength))
        {
            throw new InvalidOperationException("Partial upload HEAD did not return Upload-Length.");
        }

        var uploadOffset = ParseUploadOffset(response.Headers);
        if (uploadOffset != uploadLength)
        {
            throw new InvalidOperationException(
                $"Partial upload is incomplete ({uploadOffset} / {uploadLength} bytes).");
        }
    }

    private static string ToTusConcatReference(Uri partialUploadUri)
        => partialUploadUri.IsAbsoluteUri ? partialUploadUri.AbsolutePath : partialUploadUri.ToString();

    private static async Task UploadPartialAsync(
        HttpClient httpClient,
        Uri partialUploadUri,
        Stream sharedSource,
        object readLock,
        long sourceStartOffset,
        long partLength,
        int chunkSize,
        Action<long> onBytesUploaded,
        CancellationToken cancellationToken)
    {
        var offset = await GetUploadOffsetAsync(httpClient, partialUploadUri, cancellationToken);
        var buffer = new byte[chunkSize];

        while (offset < partLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesToRead = (int)Math.Min(chunkSize, partLength - offset);
            var bytesRead = ReadAtAbsoluteOffset(sharedSource, readLock, sourceStartOffset + offset, buffer, bytesToRead);
            if (bytesRead == 0)
            {
                throw new InvalidOperationException($"Source stream ended at part offset {offset}.");
            }

            var chunkStartOffset = offset;
            offset = await PatchChunkAsync(httpClient, partialUploadUri, offset, buffer, bytesRead, cancellationToken);
            onBytesUploaded(Math.Max(offset - chunkStartOffset, 0));
        }
    }

    private static int ReadAtAbsoluteOffset(
        Stream source,
        object readLock,
        long absoluteOffset,
        byte[] buffer,
        int count)
    {
        lock (readLock)
        {
            source.Seek(absoluteOffset, SeekOrigin.Begin);
            var totalRead = 0;
            while (totalRead < count)
            {
                var read = source.Read(buffer, totalRead, count - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }
    }

    private static List<(long StartOffset, long Length)> BuildPartRanges(long uploadSize, int partCount)
    {
        var ranges = new List<(long StartOffset, long Length)>(partCount);
        var basePartLength = uploadSize / partCount;
        var remainder = uploadSize % partCount;
        long currentStart = 0;

        for (var i = 0; i < partCount; i++)
        {
            var length = basePartLength + (i < remainder ? 1 : 0);
            ranges.Add((currentStart, length));
            currentStart += length;
        }

        return ranges;
    }

    private static async Task<long> PatchChunkAsync(
        HttpClient httpClient,
        Uri uploadUri,
        long offset,
        byte[] buffer,
        int bytesRead,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxPatchAttempts; attempt++)
        {
            try
            {
                using var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
                chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");

                using var request = new HttpRequestMessage(HttpMethod.Patch, uploadUri);
                request.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
                request.Headers.TryAddWithoutValidation("Upload-Offset", offset.ToString());
                request.Content = chunkContent;

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return ParseUploadOffset(response.Headers);
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var serverOffset = await GetUploadOffsetAsync(httpClient, uploadUri, cancellationToken);
                    if (serverOffset > offset)
                    {
                        return serverOffset;
                    }

                    offset = serverOffset;
                    continue;
                }

                if (attempt == MaxPatchAttempts || !IsTransientStatusCode(response.StatusCode))
                {
                    await EnsureSuccessAsync(response, $"TUS PATCH at offset {offset}");
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                offset = await GetUploadOffsetAsync(httpClient, uploadUri, cancellationToken);
            }
            catch (Exception ex) when (IsTransientRequestException(ex) && attempt < MaxPatchAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                offset = await GetUploadOffsetAsync(httpClient, uploadUri, cancellationToken);
            }
        }

        throw new InvalidOperationException($"TUS PATCH at offset {offset} failed after {MaxPatchAttempts} attempts.");
    }

    private static async Task<long> GetUploadOffsetAsync(
        HttpClient httpClient,
        Uri uploadUri,
        CancellationToken cancellationToken)
    {
        using var response = await SendTusRequestAsync(httpClient, HttpMethod.Head, uploadUri, cancellationToken);
        await EnsureSuccessAsync(response, "TUS HEAD", requestUri: uploadUri);
        return ParseUploadOffset(response.Headers);
    }

    private static Task<HttpResponseMessage> SendTusRequestAsync(
        HttpClient httpClient,
        HttpMethod method,
        Uri uploadUri,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, uploadUri);
        request.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        return httpClient.SendAsync(request, cancellationToken);
    }

    private static long ParseUploadOffset(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("Upload-Offset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var parsedOffset))
        {
            return parsedOffset;
        }

        throw new InvalidOperationException("TUS response did not include Upload-Offset.");
    }

    private static Uri? ResolveUploadUri(Uri tusEndpoint, Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        if (location.IsAbsoluteUri)
        {
            return location;
        }

        var relative = location.OriginalString;
        if (relative.StartsWith('/'))
        {
            return new Uri($"{tusEndpoint.GetLeftPart(UriPartial.Authority)}{relative}");
        }

        return new Uri(tusEndpoint, location);
    }

    /// <summary>
    /// Partial uploads are addressed at /tus/{fileTransferId}/partial/{partialUploadId}.
    /// Canonicalize the Location path when the server omits the literal "partial" segment.
    /// </summary>
    private static Uri CanonicalizePartialUploadPath(Uri uploadUri, string fileTransferId)
    {
        if (!TryParseTusPathSegments(uploadUri, out var segments))
        {
            return uploadUri;
        }

        if (segments.Length == 3
            && string.Equals(segments[1], PartialPathSegment, StringComparison.OrdinalIgnoreCase))
        {
            return uploadUri;
        }

        if (segments.Length == 2 && Guid.TryParse(segments[0], out _))
        {
            return BuildUriWithPath(uploadUri, $"{TusUploadPath}/{segments[0]}/{PartialPathSegment}/{segments[1]}");
        }

        if (segments.Length == 1)
        {
            return BuildUriWithPath(uploadUri, $"{TusUploadPath}/{fileTransferId}/{PartialPathSegment}/{segments[0]}");
        }

        return uploadUri;
    }

    private static Uri BuildUriWithPath(Uri baseUri, string path)
        => baseUri.IsAbsoluteUri
            ? new Uri($"{baseUri.GetLeftPart(UriPartial.Authority)}{path}")
            : new Uri(path, UriKind.Relative);

    private static bool TryParseTusPathSegments(Uri uploadUri, out string[] segments)
    {
        segments = [];
        var path = uploadUri.IsAbsoluteUri ? uploadUri.AbsolutePath : uploadUri.OriginalString;
        var prefix = $"{TusUploadPath}/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        segments = path[prefix.Length..].TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        HttpStatusCode? expectedStatusCode = null,
        Uri? requestUri = null)
    {
        if (expectedStatusCode is not null && response.StatusCode == expectedStatusCode)
        {
            return;
        }

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var target = requestUri ?? response.RequestMessage?.RequestUri;
        var targetSuffix = target is null ? string.Empty : $" for {target}";
        throw new InvalidOperationException(
            $"{operation} failed{targetSuffix} with {(int)response.StatusCode} {response.StatusCode}: {responseBody}");
    }

    private static void LogCompletion(string label, string fileTransferId, long uploadSize, TimeSpan elapsed)
    {
        var totalSeconds = Math.Max(elapsed.TotalSeconds, 0.001);
        var averageSpeedMbps = uploadSize / (1024.0 * 1024) / totalSeconds;
        Console.WriteLine(
            $"{label} completed for {fileTransferId}: " +
            $"{uploadSize / (1024.0 * 1024 * 1024):N2} GiB in {totalSeconds:N1}s (avg: {averageSpeedMbps:N2} MB/s)");
    }

    private static bool IsTransientRequestException(Exception exception)
        => exception is HttpRequestException or IOException or TaskCanceledException;

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
