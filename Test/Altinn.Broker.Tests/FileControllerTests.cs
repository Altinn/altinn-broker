using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Helpers;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Identity.Web;

using Xunit;

namespace Altinn.Broker.Tests;
public class FileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    /**
     * Inject a mock bearer configuration that does not verify anything. 
     * Generate our own JWT with correct scope, expiry and issuer. 
     * Set it as default request header
     * */

    public FileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = factory.CreateClient();
        _senderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = factory.CreateClient();
        _recipientClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_RECIPIENT_TOKEN);

        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }


    [Fact]
    public async Task WhenAllIsOk_NormalFlow_Success()
    {
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", new FileInitalizeExt()
        {
            Checksum = null,
            FileName = "input.txt",
            PropertyList = [],
            Recipients = new List<string> { "0192:986252932" },
            Sender = "0192:991825827",
            SendersFileReference = "test-data"
        });
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();

        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        Assert.True(initializedFile.FileStatus == FileStatusExt.Initialized);

        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode);
        }

        var uploadedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(uploadedFile);
        Assert.True(uploadedFile.FileStatus == FileStatusExt.Published); // When running integration test this happens instantly as of now.

        var downloadedFile = await _recipientClient.GetAsync($"broker/api/v1/file/{fileId}/download");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);

        var downloadedFileDetails = await _senderClient.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileDetails);
        Assert.True(downloadedFileDetails.FileStatus == FileStatusExt.Published);
        Assert.Contains(downloadedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadStarted);

        await _recipientClient.PostAsync($"broker/api/v1/file/{fileId}/confirmdownload", null);

        var confirmedFileDetails = await _senderClient.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(confirmedFileDetails);
        Assert.True(confirmedFileDetails.FileStatus == FileStatusExt.AllConfirmedDownloaded);
        Assert.Contains(confirmedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadConfirmed);
    }
}
