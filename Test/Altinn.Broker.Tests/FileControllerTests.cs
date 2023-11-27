using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Altinn.Broker.Tests;
public class FileControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public FileControllerTests(WebApplicationFactory<Program> factory)
    {
        factory.WithWebHostBuilder(configuration => configuration.UseSetting("ASPNETCORE_ENVIRONMENT", "Development"));
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMTkyOjk5MTgyNTgyNyJ9.exFSD-mL1fzoWhg8IKcVeCeEyJ5qpABPU9A1AXHDa_k");
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }


    [Fact]
    public async Task WhenAllIsOk_NormalFlow_Success()
    {
        var initializeFileResponse = await _client.PostAsJsonAsync("broker/api/v1/file", new FileInitalizeExt()
        {
            Checksum = null,
            FileName = "input.txt",
            PropertyList = [],
            Recipients = new List<string> { "974761076" },
            Sender = "991825827",
            SendersFileReference = "test-data"
        });
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();

        var initializedFile = await _client.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        Assert.True(initializedFile.FileStatus == FileStatusExt.Initialized);

        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _client.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode);
        }

        var uploadedFile = await _client.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(uploadedFile);
        Assert.True(uploadedFile.FileStatus == FileStatusExt.Published); // When running integration test this happens instantly as of now.

        var downloadedFile = await _client.GetAsync($"broker/api/v1/file/{fileId}/download");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);

        var downloadedFileDetails = await _client.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileDetails);
        Assert.True(downloadedFileDetails.FileStatus == FileStatusExt.Published);
        Assert.Contains(downloadedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadStarted);

        await _client.PostAsync($"broker/api/v1/file/{fileId}/confirmdownload", null);

        var confirmedFileDetails = await _client.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(confirmedFileDetails);
        Assert.True(confirmedFileDetails.FileStatus == FileStatusExt.AllConfirmedDownloaded);
        Assert.Contains(confirmedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadConfirmed);
    }
}
