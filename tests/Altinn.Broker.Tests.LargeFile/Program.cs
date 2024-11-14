using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.API.Models;
using Altinn.Broker.Models;

namespace Altinn.Broker.Tests.LargeFile;

public class Program
{
    private const int BufferSize = 1024*1024*1;
    private const string testResource = "altinn-broker-test-resource-2";

    static async Task Main(string[] args)
    {
        string? baseUrl = Environment.GetEnvironmentVariable("BASE_URL");
        string? username = Environment.GetEnvironmentVariable("TEST_TOOLS_USERNAME");
        string? password = Environment.GetEnvironmentVariable("TEST_TOOLS_PASSWORD");
        int gbsToUpload = Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD") is not null ? int.Parse(Environment.GetEnvironmentVariable("GIGABYTES_TO_UPLOAD")) : 100;
        long uploadSize = gbsToUpload * 1024L * 1024 * 1024;
        Console.WriteLine($"BASE_URL: {baseUrl}");
        Console.WriteLine($"GIGABYTES_TO_UPLOAD: {gbsToUpload}");

        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = GetRequiredInput(
                "Enter the base URL"
            );
        }
        if (string.IsNullOrEmpty(username))
        {
            username = GetRequiredInput("Enter the test tools username");
        }
        if (string.IsNullOrEmpty(password))
        {
            password = GetRequiredInput("Enter the test tools password");
        }
        Console.WriteLine($"Writing {uploadSize / (1024.0 * 1024.0 * 1024.0):N2} GiB with a buffer size of {BufferSize / (1024.0):N2} KiB");
        using var httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromHours(48)
        };
        var token = await GetAccessToken(httpClient, username, password, "991825827");
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        await ConfigureResource(httpClient, baseUrl, uploadSize);
        var fileTransferId = await InitializeFileTransfer(httpClient, baseUrl);
        await UploadFileToBroker(httpClient, fileTransferId, baseUrl, uploadSize);
    }

    private static string GetRequiredInput(string prompt, string? defaultValue = null)
    {
        while (true)
        {
            Console.Write($"{prompt}{(defaultValue != null ? $" (default: {defaultValue})" : "")}: ");
            var input = Console.ReadLine()?.Trim();

            if (!string.IsNullOrEmpty(input))
                return input;

            if (defaultValue != null)
                return defaultValue;

            Console.WriteLine("This value is required. Please try again.");
        }
    }


    private static async Task<string> GetAccessToken(HttpClient httpClient, string testToolsUsername, string testToolsPassword, string orgNumber)
    {
        var httpRequestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri($"https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env=tt02&scopes=altinn:broker.write altinn:resourceregistry/resource.write&org=ttd&orgNo={orgNumber}")
        };
        var authenticationString = $"{testToolsUsername}:{testToolsPassword}";
        var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authenticationString));
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
        var response = await httpClient.SendAsync(httpRequestMessage);
        var responseContent = await response.Content.ReadAsStringAsync();
        return responseContent;
    }

    private static async Task ConfigureResource(HttpClient httpClient, string baseUrl, long uploadSize)
    {
        var configureResourceBody = new ResourceExt()
        {
            MaxFileTransferSize = uploadSize + 1
        };
        var httpRequestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri(baseUrl + "/broker/api/v1/resource"),
            Method = HttpMethod.Put,
            Content = new StringContent(JsonSerializer.Serialize(configureResourceBody), Encoding.UTF8, "application/json")
        };
        await httpClient.SendAsync(httpRequestMessage);
    }

    private static async Task<string> InitializeFileTransfer(HttpClient httpClient, string baseUrl)
    {
        var initializeRequestBody = BasicFileTransfer();
        var httpRequestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri(baseUrl + "/broker/api/v1/filetransfer"),
            Method = HttpMethod.Post,
            Content = new StringContent(JsonSerializer.Serialize(initializeRequestBody), Encoding.UTF8, "application/json")
        }; 
        var response = await httpClient.SendAsync(httpRequestMessage);
        var responseContent = await response.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        return responseContent.FileTransferId.ToString();
    }

    private static async Task UploadFileToBroker(HttpClient httpClient, string fileTransferId, string baseUrl, long uploadSize)
    {
        using var randomDataStream = new PseudoRandomDataStream(uploadSize);
        using var content = new StreamContent(randomDataStream, BufferSize);
        Console.WriteLine("Starting upload...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = uploadSize;
        try
        {
            var response = await httpClient.PostAsync(baseUrl + $"/broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Console.WriteLine($"Response: {response.StatusCode}");
            Console.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");
            double uploadSpeedMBps = uploadSize / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
            Console.WriteLine($"Upload stats for {fileTransferId}: " +
                $"{uploadSize / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSpeedMBps:N2} MB/s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}: {ex.StackTrace}");
        }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"Upload completed in {stopwatch.ElapsedMilliseconds.ToString("N0")} ms");
        }
    }

    private static FileTransferInitalizeExt BasicFileTransfer() => new FileTransferInitalizeExt()
    {
        ResourceId = testResource,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932" },
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data",
        DisableVirusScan = true
    };
}
