using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.API.Models;
using Altinn.Broker.Models;

namespace Altinn.Broker.Tests.LargeFile;

public class Program
{
    private const int DefaultChunkSizeMb = 8;
    private const string TestResource = "altinn-broker-test-resource-2";

    static async Task Main(string[] args)
    {
        string? baseUrl = Environment.GetEnvironmentVariable("BASE_URL");
        string? username = Environment.GetEnvironmentVariable("TEST_TOOLS_USERNAME");
        string? password = Environment.GetEnvironmentVariable("TEST_TOOLS_PASSWORD");
        int gbsToUpload = Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD") is not null
            ? int.Parse(Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD")!)
            : 10;
        int chunkSizeMb = Environment.GetEnvironmentVariable("CHUNK_SIZE_MB") is not null
            ? int.Parse(Environment.GetEnvironmentVariable("CHUNK_SIZE_MB")!)
            : DefaultChunkSizeMb;
        int parallelPartialUploads = Environment.GetEnvironmentVariable("TUS_PARALLEL_PARTIAL_UPLOADS") is not null
            ? int.Parse(Environment.GetEnvironmentVariable("TUS_PARALLEL_PARTIAL_UPLOADS")!)
            : 1;
        long uploadSize = gbsToUpload * 1024L * 1024 * 1024;
        int chunkSize = chunkSizeMb * 1024 * 1024;

        Console.WriteLine($"BASE_URL: {baseUrl}");
        Console.WriteLine($"GIGABYTES_TO_UPLOAD: {gbsToUpload}");
        Console.WriteLine($"CHUNK_SIZE_MB: {chunkSizeMb}");
        Console.WriteLine($"TUS_PARALLEL_PARTIAL_UPLOADS: {parallelPartialUploads}");

        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = GetRequiredInput("Enter the base URL");
        }
        if (string.IsNullOrEmpty(username))
        {
            username = GetRequiredInput("Enter the test tools username");
        }
        if (string.IsNullOrEmpty(password))
        {
            password = GetRequiredInput("Enter the test tools password");
        }

        Console.WriteLine(
            $"Uploading {uploadSize / (1024.0 * 1024.0 * 1024.0):N2} GiB via TUS with {chunkSizeMb} MiB chunks");

        using var uploadHandler = new SocketsHttpHandler
        {
            // Reduce transient socket failures under parallel PATCH pressure.
            MaxConnectionsPerServer = 200,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };
        using var tokenHandler = new SocketsHttpHandler();
        var tokenProvider = new AccessTokenProvider(
            cancellationToken => GetAccessToken(tokenHandler, username, password, "991825827", cancellationToken));
        using var httpClient = new HttpClient(new BearerTokenHandler(tokenProvider)
        {
            InnerHandler = uploadHandler
        })
        {
            // Each TUS PATCH is a short request; no multi-hour connection is required.
            Timeout = TimeSpan.FromMinutes(30)
        };

        await ConfigureResource(httpClient, baseUrl, uploadSize);
        var fileTransferId = await InitializeFileTransfer(httpClient, baseUrl);

        using var randomDataStream = new PseudoRandomDataStream(uploadSize);
        if (parallelPartialUploads > 1)
        {
            await TusUploader.UploadWithConcatenationAsync(
                httpClient,
                baseUrl,
                fileTransferId,
                randomDataStream,
                uploadSize,
                chunkSize,
                parallelPartialUploads,
                tokenProvider);
        }
        else
        {
            await TusUploader.UploadAsync(httpClient, baseUrl, fileTransferId, randomDataStream, uploadSize, chunkSize, tokenProvider);
        }
    }

    private static string GetRequiredInput(string prompt, string? defaultValue = null)
    {
        while (true)
        {
            Console.Write($"{prompt}{(defaultValue != null ? $" (default: {defaultValue})" : "")}: ");
            var input = Console.ReadLine()?.Trim();

            if (!string.IsNullOrEmpty(input))
            {
                return input;
            }

            if (defaultValue != null)
            {
                return defaultValue;
            }

            Console.WriteLine("This value is required. Please try again.");
        }
    }

    private static async Task<string> GetAccessToken(
        HttpMessageHandler httpMessageHandler,
        string testToolsUsername,
        string testToolsPassword,
        string orgNumber,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient(httpMessageHandler, disposeHandler: false);
        using var httpRequestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(
                $"https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env=tt02&scopes=altinn:broker.write altinn:serviceowner&org=ttd&orgNo={orgNumber}")
        };
        var authenticationString = $"{testToolsUsername}:{testToolsPassword}";
        var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
        using var response = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task ConfigureResource(HttpClient httpClient, string baseUrl, long uploadSize)
    {
        var configureResourceBody = new ResourceExt
        {
            MaxFileTransferSize = uploadSize + 1
        };
        using var httpRequestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(baseUrl + "/broker/api/v1/resource/" + TestResource),
            Method = HttpMethod.Put,
            Content = new StringContent(JsonSerializer.Serialize(configureResourceBody), Encoding.UTF8, "application/json")
        };
        using var response = await httpClient.SendAsync(httpRequestMessage);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Configure resource returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static async Task<string> InitializeFileTransfer(HttpClient httpClient, string baseUrl)
    {
        using var httpRequestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(baseUrl + "/broker/api/v1/filetransfer"),
            Method = HttpMethod.Post,
            Content = new StringContent(JsonSerializer.Serialize(BasicFileTransfer()), Encoding.UTF8, "application/json")
        };
        using var response = await httpClient.SendAsync(httpRequestMessage);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Got " + response.StatusCode + " response. Body was " + await response.Content.ReadAsStringAsync());
        }
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>()
            ?? throw new InvalidOperationException("Initialize response was empty.");
        return responseContent.FileTransferId.ToString();
    }

    private static FileTransferInitalizeExt BasicFileTransfer() => new()
    {
        ResourceId = TestResource,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = ["0192:986252932"],
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data",
        DisableVirusScan = true
    };
}
