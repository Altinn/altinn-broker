using System.Net;

namespace Altinn.Broker.LargeFileLoadTester;

public class Program
{
    private const int BufferSize = 65536; 
    private const long TotalBytes = 1024L * 1024 * 1024 * 1; // 1 GB data to upload


    private const string baseUrl = "";
    private const string token = "";
    private const string fileTransferId = "";

    private static string fileUploadUrl = baseUrl + $"/broker/api/v1/filetransfer/{fileTransferId}/upload";

    static async Task Main(string[] args)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; 
        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            httpClient.Timeout = TimeSpan.FromHours(48);

            Console.WriteLine("Starting upload...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using (var randomDataStream = new XorShiftDataStream(TotalBytes, BufferSize)) { 
                using (var content = new StreamContent(randomDataStream))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    content.Headers.ContentLength = TotalBytes;
                    try
                    {
                        var response = await httpClient.PostAsync(baseUrl + $"/broker/api/v1/filetransfer/{fileTransferId}/upload", content, new CancellationToken());
                        Console.WriteLine($"Response: {response.StatusCode}");
                    }
                    catch (Exception ex)
                    {
                        httpClient.Dispose();
                        Console.WriteLine(ex.Message);
                    }
                    finally { 
                        stopwatch.Stop();
                        Console.WriteLine($"Upload completed in {stopwatch.ElapsedMilliseconds.ToString("N0")} ms");
                        Console.WriteLine($"Transferred {randomDataStream.Position.ToString("N0")} bytes");
                    }
                }
            }
        }
    }
}
