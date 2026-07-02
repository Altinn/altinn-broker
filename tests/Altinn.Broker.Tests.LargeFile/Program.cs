using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.API.Models;
using Altinn.Broker.Models;

namespace Altinn.Broker.Tests.LargeFile;

public class Program
{
    private const int DefaultChunkSizeMb = 32;
    private const string TestResource = "altinn-broker-test-resource-2";

    static async Task Main(string[] args)
    {
        string? baseUrl = "https://altinn-dev-api.azure-api.net"; // Environment.GetEnvironmentVariable("BASE_URL");
        string? username = "autotest"; // Environment.GetEnvironmentVariable("TEST_TOOLS_USERNAME");
        string? password = "altinn8900bnn"; // Environment.GetEnvironmentVariable("TEST_TOOLS_PASSWORD");
        int gbsToUpload = Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD") is not null
            ? int.Parse(Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD")!)
            : 10;
        int chunkSizeMb = Environment.GetEnvironmentVariable("CHUNK_SIZE_MB") is not null
            ? int.Parse(Environment.GetEnvironmentVariable("CHUNK_SIZE_MB")!)
            : DefaultChunkSizeMb;
        long uploadSize = gbsToUpload * 1024L * 1024 * 1024;
        int chunkSize = chunkSizeMb * 1024 * 1024;

        Console.WriteLine($"BASE_URL: {baseUrl}");
        Console.WriteLine($"GIGABYTES_TO_UPLOAD: {gbsToUpload}");
        Console.WriteLine($"CHUNK_SIZE_MB: {chunkSizeMb}");

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

        using var httpClient = new HttpClient
        {
            // Each TUS PATCH is a short request; no multi-hour connection is required.
            Timeout = TimeSpan.FromMinutes(30)
        };
        var token = await GetAccessToken(httpClient, username, password, "991825827");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await ConfigureResource(httpClient, baseUrl, uploadSize);
        var fileTransferId = await InitializeFileTransfer(httpClient, baseUrl);

        using var randomDataStream = new PseudoRandomDataStream(uploadSize);
        await TusUploader.UploadAsync(httpClient, baseUrl, fileTransferId, randomDataStream, uploadSize, chunkSize);
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
        HttpClient httpClient,
        string testToolsUsername,
        string testToolsPassword,
        string orgNumber)
    {
        using var httpRequestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(
                $"https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env=tt02&scopes=altinn:broker.write altinn:serviceowner&org=ttd&orgNo={orgNumber}")
        };
        var authenticationString = $"{testToolsUsername}:{testToolsPassword}";
        var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
        using var response = await httpClient.SendAsync(httpRequestMessage);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task ConfigureResource(HttpClient httpClient, string baseUrl, long uploadSize)
    {
        var configureResourceBody = new ResourceExt
        {
            MaxFileTransferSize = uploadSize + 1
        };
        using var httpRequestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(baseUrl + "/broker/api/v1/resource"),
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
