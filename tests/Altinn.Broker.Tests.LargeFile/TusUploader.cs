using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace Altinn.Broker.Tests.LargeFile;

public static class TusUploader
{
    private const string TusVersion = "1.0.0";

    private sealed record TusServerCapabilities(bool SupportsConcatenation);

    public static async Task UploadAsync(
        HttpClient httpClient,
        string baseUrl,
        string fileTransferId,
        Stream source,
        long uploadSize,
        int chunkSize,
        CancellationToken cancellationToken = default)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be greater than zero.");
        }

        var tusEndpoint = BuildTusEndpointUri(baseUrl, fileTransferId);
        await EnsureServerSupportsTus(httpClient, tusEndpoint, cancellationToken);

        var uploadUri = await CreateUploadAsync(httpClient, tusEndpoint, fileTransferId, uploadSize, cancellationToken);
        var offset = await GetUploadOffsetAsync(httpClient, uploadUri, cancellationToken);
        if (offset > 0)
        {
            Console.WriteLine($"Resuming upload at offset {offset:N0} ({offset * 100.0 / uploadSize:F3}%)");
            source.Seek(offset, SeekOrigin.Begin);
        }

        var progress = new UploadProgress(uploadSize, offset);
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
                throw new InvalidOperationException(
                    $"Source stream ended unexpectedly at offset {offset} (expected {uploadSize} bytes).");
            }

            var chunkStartOffset = offset;
            offset = await PatchChunkAsync(httpClient, uploadUri, chunkStartOffset, buffer, bytesRead, cancellationToken);
            var expectedOffset = chunkStartOffset + bytesRead;
            if (offset != expectedOffset)
            {
                if (!source.CanSeek)
                {
                    throw new InvalidOperationException(
                        $"Server reported offset {offset}, expected {expectedOffset}, but the source stream cannot be realigned.");
                }

                source.Seek(offset, SeekOrigin.Begin);
            }

            progress.Update(offset);

            if (offset > uploadSize)
            {
                throw new InvalidOperationException(
                    $"Server reported offset {offset} which exceeds upload length {uploadSize}.");
            }
        }

        totalStopwatch.Stop();
        var totalSeconds = Math.Max(totalStopwatch.Elapsed.TotalSeconds, 0.001);
        var averageSpeedMbps = uploadSize / (1024.0 * 1024) / totalSeconds;
        Console.WriteLine(
            $"TUS upload completed for {fileTransferId}: " +
            $"{uploadSize / (1024.0 * 1024 * 1024):N2} GiB in {totalSeconds:N1}s (avg: {averageSpeedMbps:N2} MB/s)");
    }

    // Reference implementation for TUS concatenation extension.
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
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be greater than zero.");
        }

        if (parallelPartialUploads <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parallelPartialUploads),
                parallelPartialUploads,
                "Use a value greater than 1 for concatenation uploads.");
        }

        if (!source.CanSeek)
        {
            throw new InvalidOperationException("Concatenation upload requires a seekable source stream.");
        }

        var tusEndpoint = BuildTusEndpointUri(baseUrl, fileTransferId);
        var capabilities = await EnsureServerSupportsTus(httpClient, tusEndpoint, cancellationToken);
        if (!capabilities.SupportsConcatenation)
        {
            throw new InvalidOperationException(
                "Server does not advertise TUS concatenation support (Tus-Extension missing 'concatenation').");
        }

        var partRanges = BuildPartRanges(uploadSize, parallelPartialUploads);
        var partialUris = new Uri[partRanges.Count];
        for (var i = 0; i < partRanges.Count; i++)
        {
            partialUris[i] = await CreatePartialUploadAsync(
                httpClient,
                tusEndpoint,
                partRanges[i].Length,
                cancellationToken);
        }

        var readLock = new object();
        var progress = new UploadProgress(uploadSize, 0);
        using var progressTimer = new Timer(_ => progress.LogProgress(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var totalStopwatch = Stopwatch.StartNew();
        long progressBytes = 0;

        await Parallel.ForAsync(0, partRanges.Count, cancellationToken, async (partIndex, ct) =>
        {
            var part = partRanges[partIndex];
            var uploadedBytes = await UploadPartialAsync(
                httpClient,
                partialUris[partIndex],
                source,
                readLock,
                part.StartOffset,
                part.Length,
                chunkSize,
                ct);
            progress.Update(Interlocked.Add(ref progressBytes, uploadedBytes));
        });

        var finalUri = await CreateFinalUploadAsync(httpClient, tusEndpoint, partialUris, cancellationToken);
        var finalOffset = await GetUploadOffsetAsync(httpClient, finalUri, cancellationToken);
        if (finalOffset != uploadSize)
        {
            throw new InvalidOperationException(
                $"Concatenated upload completed with unexpected offset {finalOffset}, expected {uploadSize}.");
        }

        totalStopwatch.Stop();
        var totalSeconds = Math.Max(totalStopwatch.Elapsed.TotalSeconds, 0.001);
        var averageSpeedMbps = uploadSize / (1024.0 * 1024) / totalSeconds;
        Console.WriteLine(
            $"TUS concatenation upload completed for {fileTransferId}: " +
            $"{uploadSize / (1024.0 * 1024 * 1024):N2} GiB in {totalSeconds:N1}s (avg: {averageSpeedMbps:N2} MB/s)");
    }

    private sealed class UploadProgress(long totalSize, long initialOffset)
    {
        private long _offset = initialOffset;
        private readonly Stopwatch _intervalStopwatch = Stopwatch.StartNew();

        public void Update(long offset)
        {
            Interlocked.Exchange(ref _offset, offset);
        }

        public void LogProgress()
        {
            var currentOffset = Interlocked.Read(ref _offset);
            var elapsedSeconds = Math.Max(_intervalStopwatch.Elapsed.TotalSeconds, 0.001);
            Console.WriteLine(
                $"Progress: {currentOffset * 100.0 / totalSize:F3}% " +
                $"({currentOffset / (1024.0 * 1024 * 1024):N2} GiB / {totalSize / (1024.0 * 1024 * 1024):N2} GiB) " +
                $"avg {currentOffset / elapsedSeconds / (1024 * 1024):N2} MiB/s");
        }
    }

    private static Uri BuildTusEndpointUri(string baseUrl, string fileTransferId)
        => new($"{baseUrl.TrimEnd('/')}/broker/api/v1/filetransfer/upload/tus/{fileTransferId}");

    private static async Task<TusServerCapabilities> EnsureServerSupportsTus(HttpClient httpClient, Uri tusEndpoint, CancellationToken cancellationToken)
    {
        using var optionsRequest = new HttpRequestMessage(HttpMethod.Options, tusEndpoint);
        optionsRequest.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);

        using var optionsResponse = await httpClient.SendAsync(optionsRequest, cancellationToken);
        var responseBody = await optionsResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!optionsResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"TUS OPTIONS failed with {(int)optionsResponse.StatusCode} {optionsResponse.StatusCode}: {responseBody}");
        }

        if (!optionsResponse.Headers.Contains("Tus-Resumable"))
        {
            throw new InvalidOperationException("Server did not return Tus-Resumable header — is the TUS endpoint enabled?");
        }

        var supportsConcatenation = optionsResponse.Headers.TryGetValues("Tus-Extension", out var extensionHeaderValues)
            && extensionHeaderValues
                .SelectMany(static value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Any(static extension => string.Equals(extension, "concatenation", StringComparison.OrdinalIgnoreCase));

        return new TusServerCapabilities(supportsConcatenation);
    }

    private static async Task<Uri> CreateUploadAsync(
        HttpClient httpClient,
        Uri tusEndpoint,
        string fileTransferId,
        long uploadSize,
        CancellationToken cancellationToken)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, tusEndpoint);
        createRequest.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        createRequest.Headers.TryAddWithoutValidation("Upload-Length", uploadSize.ToString());

        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        var responseBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);

        if (createResponse.StatusCode == HttpStatusCode.Created)
        {
            return ResolveUploadUri(tusEndpoint, createResponse.Headers.Location)
                ?? throw new InvalidOperationException("TUS POST succeeded but no Location header was returned.");
        }

        if (createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            Console.WriteLine("Upload resource already exists — resuming via HEAD.");
            return tusEndpoint;
        }

        throw new InvalidOperationException(
            $"TUS POST failed with {(int)createResponse.StatusCode} {createResponse.StatusCode}: {responseBody}");
    }

    private static async Task<Uri> CreatePartialUploadAsync(
        HttpClient httpClient,
        Uri tusEndpoint,
        long partLength,
        CancellationToken cancellationToken)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, tusEndpoint);
        createRequest.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        createRequest.Headers.TryAddWithoutValidation("Upload-Length", partLength.ToString());
        createRequest.Headers.TryAddWithoutValidation("Upload-Concat", "partial");

        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        var responseBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        if (createResponse.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"TUS partial POST failed with {(int)createResponse.StatusCode} {createResponse.StatusCode}: {responseBody}");
        }

        return ResolveUploadUri(tusEndpoint, createResponse.Headers.Location)
            ?? throw new InvalidOperationException("TUS partial POST succeeded but no Location header was returned.");
    }

    private static async Task<Uri> CreateFinalUploadAsync(
        HttpClient httpClient,
        Uri tusEndpoint,
        IReadOnlyList<Uri> partialUris,
        CancellationToken cancellationToken)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, tusEndpoint);
        createRequest.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
        createRequest.Headers.TryAddWithoutValidation(
            "Upload-Concat",
            $"final;{string.Join(' ', partialUris.Select(static uri => uri.ToString()))}");

        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        var responseBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        if (createResponse.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"TUS final POST failed with {(int)createResponse.StatusCode} {createResponse.StatusCode}: {responseBody}");
        }

        return ResolveUploadUri(tusEndpoint, createResponse.Headers.Location)
            ?? throw new InvalidOperationException("TUS final POST succeeded but no Location header was returned.");
    }

    private static async Task<long> UploadPartialAsync(
        HttpClient httpClient,
        Uri partialUploadUri,
        Stream sharedSource,
        object readLock,
        long sourceStartOffset,
        long partLength,
        int chunkSize,
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
                throw new InvalidOperationException(
                    $"Source stream ended unexpectedly at part offset {offset} (expected {partLength} bytes).");
            }

            offset = await PatchChunkAsync(httpClient, partialUploadUri, offset, buffer, bytesRead, cancellationToken);
        }

        return partLength;
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
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
            chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");

            using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, uploadUri);
            patchRequest.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);
            patchRequest.Headers.TryAddWithoutValidation("Upload-Offset", offset.ToString());
            patchRequest.Content = chunkContent;

            using var patchResponse = await httpClient.SendAsync(patchRequest, cancellationToken);
            if (patchResponse.StatusCode == HttpStatusCode.NoContent)
            {
                return ParseUploadOffset(patchResponse.Headers);
            }

            var responseBody = await patchResponse.Content.ReadAsStringAsync(cancellationToken);
            if (patchResponse.StatusCode == HttpStatusCode.Conflict)
            {
                var serverOffset = await GetUploadOffsetAsync(httpClient, uploadUri, cancellationToken);
                if (serverOffset > offset)
                {
                    Console.WriteLine($"Upload-Offset conflict — server is at {serverOffset}, resuming.");
                    return serverOffset;
                }

                if (serverOffset < offset)
                {
                    Console.WriteLine(
                        $"Upload-Offset conflict — server is behind at {serverOffset}, waiting for async commit to reach {offset}.");
                }
            }

            if (attempt == 5 || !IsTransientStatusCode(patchResponse.StatusCode))
            {
                throw new InvalidOperationException(
                    $"TUS PATCH failed at offset {offset} with {(int)patchResponse.StatusCode} {patchResponse.StatusCode}: {responseBody}");
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            Console.WriteLine(
                $"TUS PATCH at offset {offset} failed ({patchResponse.StatusCode}), retrying in {delay.TotalSeconds:N0}s (attempt {attempt}/5)...");
            await Task.Delay(delay, cancellationToken);
        }

        throw new UnreachableException();
    }

    private static async Task<long> GetUploadOffsetAsync(
        HttpClient httpClient,
        Uri uploadUri,
        CancellationToken cancellationToken)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, uploadUri);
        headRequest.Headers.TryAddWithoutValidation("Tus-Resumable", TusVersion);

        using var headResponse = await httpClient.SendAsync(headRequest, cancellationToken);
        var responseBody = await headResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!headResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"TUS HEAD failed with {(int)headResponse.StatusCode} {headResponse.StatusCode}: {responseBody}");
        }

        return ParseUploadOffset(headResponse.Headers);
    }

    private static long ParseUploadOffset(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("Upload-Offset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var parsedOffset))
        {
            return parsedOffset;
        }

        throw new InvalidOperationException("TUS response did not include a valid Upload-Offset header.");
    }

    private static Uri? ResolveUploadUri(Uri tusEndpoint, Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        return location.IsAbsoluteUri ? location : new Uri(tusEndpoint, location);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.Conflict
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
