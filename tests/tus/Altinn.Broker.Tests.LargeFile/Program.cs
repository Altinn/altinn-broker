using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Broker.API.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Tests.LargeFile;

public class Program
{
    private const int DefaultChunkSizeMb = 8;
    private const int DefaultParallelPartialUploads = 4;
    private const int DefaultUploadMiB = 64;

    private const int DefaultPublishVerificationTimeoutMinutes = 240;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    static async Task Main(string[] args)
    {
        var authOptions = AltinnAuthClient.ReadOptionsFromEnvironment();
        var baseUrl = authOptions.BaseUrl;
        var resourceId = RequireEnv("RESOURCE_ID");
        var chunkSizeMb = int.Parse(ReadEnv("CHUNK_SIZE_MB", DefaultChunkSizeMb.ToString())!);
        var parallelPartialUploads = int.Parse(
            ReadEnv("TUS_PARALLEL_PARTIAL_UPLOADS", DefaultParallelPartialUploads.ToString())!);
        var uploadFilePath = ReadEnv("UPLOAD_FILE_PATH", null);
        var chunkSize = chunkSizeMb * 1024 * 1024;

        Console.WriteLine($"BASE_URL: {baseUrl}");
        Console.WriteLine($"RESOURCE_ID: {resourceId}");
        Console.WriteLine($"ORG_NO: {authOptions.OrgNumber}");
        Console.WriteLine($"CHUNK_SIZE_MB: {chunkSizeMb}");
        Console.WriteLine($"TUS_PARALLEL_PARTIAL_UPLOADS: {parallelPartialUploads}");

        using var authHttpClient = new HttpClient(new SocketsHttpHandler());
        var token = await AltinnAuthClient.ExchangeAltinnTokenAsync(authHttpClient, authOptions);
        using var httpClient = CreateUploadHttpClient(token);

        var fileTransferId = await InitializeFileTransfer(
            httpClient,
            baseUrl,
            authOptions.OrgNumber,
            resourceId);
        Console.WriteLine($"File transfer id: {fileTransferId}");

        if (uploadFilePath is not null)
        {
            var path = Path.GetFullPath(uploadFilePath);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"UPLOAD_FILE_PATH does not exist or is not a file: {uploadFilePath}");
            }

            await using var fileStream = File.OpenRead(path);
            var uploadSize = fileStream.Length;
            LogUploadSize(uploadSize);
            Console.WriteLine($"UPLOAD_FILE_PATH: {path}");

            await UploadAsync(
                httpClient,
                baseUrl,
                fileTransferId,
                fileStream,
                uploadSize,
                chunkSize,
                parallelPartialUploads);
        }
        else
        {
            var uploadSize = Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD") is { Length: > 0 } gigabytes
                ? long.Parse(gigabytes) * 1024L * 1024 * 1024
                : DefaultUploadMiB * 1024L * 1024;
            LogUploadSize(uploadSize);

            using var randomDataStream = new PseudoRandomDataStream(uploadSize);
            await UploadAsync(
                httpClient,
                baseUrl,
                fileTransferId,
                randomDataStream,
                uploadSize,
                chunkSize,
                parallelPartialUploads);
        }

        await VerifyPublishedAsync(authHttpClient, authOptions, baseUrl, fileTransferId);
    }

    private static void LogUploadSize(long uploadSize)
    {
        Console.WriteLine(
            $"Upload size: {uploadSize / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSize / (1024.0 * 1024.0):N2} MiB)");
    }

    private static async Task UploadAsync(
        HttpClient httpClient,
        string baseUrl,
        string fileTransferId,
        Stream source,
        long uploadSize,
        int chunkSize,
        int parallelPartialUploads)
    {
        if (parallelPartialUploads > 1)
        {
            await TusUploader.UploadWithConcatenationAsync(
                httpClient,
                baseUrl,
                fileTransferId,
                source,
                uploadSize,
                chunkSize,
                parallelPartialUploads);
        }
        else
        {
            await TusUploader.UploadAsync(
                httpClient,
                baseUrl,
                fileTransferId,
                source,
                uploadSize,
                chunkSize);
        }
    }

    private static string? ReadEnv(string name, string? fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Missing required environment variable: {name}");
        }

        return value;
    }

    private static HttpClient CreateUploadHttpClient(string token)
    {
        var uploadHandler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 200,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        return new HttpClient(new BearerTokenHandler(token)
        {
            InnerHandler = uploadHandler
        })
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };
    }

    private static async Task<string> InitializeFileTransfer(
        HttpClient httpClient,
        string baseUrl,
        string orgNumber,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var payload = BasicFileTransfer(orgNumber, resourceId);
        using var httpRequestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(baseUrl.TrimEnd('/') + "/broker/api/v1/filetransfer"),
            Method = HttpMethod.Post,
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };
        httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Initialize file transfer failed with {(int)response.StatusCode} {response.StatusCode}: {responseBody}");
        }

        var responseContent = JsonSerializer.Deserialize<FileTransferInitializeResponseExt>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Initialize response was empty.");
        if (responseContent.FileTransferId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Initialize response did not include fileTransferId. Body: {responseBody}");
        }

        return responseContent.FileTransferId.ToString();
    }

    private static async Task VerifyPublishedAsync(
        HttpClient authHttpClient,
        AltinnAuthOptions authOptions,
        string baseUrl,
        string fileTransferId,
        CancellationToken cancellationToken = default)
    {
        var tokenHolder = new AltinnTokenHolder(authHttpClient, authOptions);
        var timeoutMinutes = int.Parse(
            ReadEnv("PUBLISH_VERIFICATION_TIMEOUT_MINUTES", DefaultPublishVerificationTimeoutMinutes.ToString())!);
        var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var overview = await GetFileTransferOverviewAsync(
                authHttpClient,
                tokenHolder,
                baseUrl,
                fileTransferId,
                cancellationToken);

            if (overview.FileTransferStatus == FileTransferStatusExt.Published)
            {
                Console.WriteLine($"Verified file transfer {fileTransferId} is Published.");
                return;
            }

            if (overview.FileTransferStatus == FileTransferStatusExt.Failed)
            {
                throw new InvalidOperationException(
                    $"File transfer {fileTransferId} failed before reaching Published status.");
            }

            Console.WriteLine(
                $"Waiting for Published status (current: {overview.FileTransferStatus}). Retrying...");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        var finalOverview = await GetFileTransferOverviewAsync(
            authHttpClient,
            tokenHolder,
            baseUrl,
            fileTransferId,
            cancellationToken);
        throw new InvalidOperationException(
            $"File transfer {fileTransferId} did not reach Published within {timeoutMinutes} minutes. Last status: {finalOverview.FileTransferStatus}");
    }

    private static async Task<FileTransferOverviewExt> GetFileTransferOverviewAsync(
        HttpClient authHttpClient,
        AltinnTokenHolder tokenHolder,
        string baseUrl,
        string fileTransferId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/broker/api/v1/filetransfer/{fileTransferId}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var token = await tokenHolder.GetValidTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await authHttpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                await tokenHolder.RefreshTokenAsync(cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Overview request failed with {(int)response.StatusCode} {response.StatusCode}: {responseBody}");
            }

            return JsonSerializer.Deserialize<FileTransferOverviewExt>(responseBody, JsonOptions)
                ?? throw new InvalidOperationException("Overview response was empty.");
        }

        throw new InvalidOperationException("Overview request failed after token refresh.");
    }

    private static FileTransferInitalizeExt BasicFileTransfer(string orgNumber, string resourceId) => new()
    {
        ResourceId = resourceId,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = ["0192:310880442"],
        Sender = FormatPartyId(orgNumber),
        SendersFileTransferReference = "test-data",
        DisableVirusScan = true,
    };

    private static string FormatPartyId(string orgNumber)
        => orgNumber.Contains(':') ? orgNumber : $"0192:{orgNumber}";
}
