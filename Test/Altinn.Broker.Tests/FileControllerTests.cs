using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.AspNetCore.Mvc.Testing;

using Xunit;

namespace Altinn.Broker.Tests;
public class FileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

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
    public async Task NormalFlow_WhenAllIsOK_Success()
    {
        // Initialize
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.Equal(System.Net.HttpStatusCode.OK, initializeFileResponse.StatusCode);
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var fileAfterInitialize = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(fileAfterInitialize);
        Assert.True(fileAfterInitialize.FileStatus == FileStatusExt.Initialized);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode);
        }
        var fileAfterUpload = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(fileAfterUpload);
        Assert.True(fileAfterUpload.FileStatus == FileStatusExt.Published); // When running integration test this happens instantly as of now.

        // Download
        var downloadedFile = await _recipientClient.GetAsync($"broker/api/v1/file/{fileId}/download");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);

        // Details
        var downloadedFileDetails = await _senderClient.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileDetails);
        Assert.True(downloadedFileDetails.FileStatus == FileStatusExt.Published);
        Assert.Contains(downloadedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadStarted);

        // Confirm
        await _recipientClient.PostAsync($"broker/api/v1/file/{fileId}/confirmdownload", null);
        var confirmedFileDetails = await _senderClient.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(confirmedFileDetails);
        Assert.True(confirmedFileDetails.FileStatus == FileStatusExt.AllConfirmedDownloaded);
        Assert.Contains(confirmedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadConfirmed);
    }

    [Fact]
    public async Task DownloadFile_WhenFileDownloadsTwice_ShowLastOccurenceInOverview()
    {
        // Arrange
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode);
        }
        var uploadedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);

        // Act
        var downloadedFile1 = await _recipientClient.GetAsync($"broker/api/v1/file/{fileId}/download");
        await Task.Delay(1000);
        var downloadedFile2 = await _recipientClient.GetAsync($"broker/api/v1/file/{fileId}/download");
        var downloadedFile1Bytes = await downloadedFile1.Content.ReadAsByteArrayAsync();
        var downloadedFile2Bytes = await downloadedFile2.Content.ReadAsByteArrayAsync();
        Assert.Equal(downloadedFile1Bytes, downloadedFile2Bytes);

        // Assert
        var downloadedFileDetails = await _senderClient.GetFromJsonAsync<FileStatusDetailsExt>($"broker/api/v1/file/{fileId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileDetails);
        Assert.Contains(downloadedFileDetails.RecipientFileStatusHistory, recipient => recipient.RecipientFileStatusCode == RecipientFileStatusExt.DownloadStarted);
        var downloadStartedEvents = downloadedFileDetails.RecipientFileStatusHistory.Where(recipientFileStatus => recipientFileStatus.RecipientFileStatusCode == RecipientFileStatusExt.DownloadStarted);
        Assert.NotNull(downloadStartedEvents);
        Assert.Equal(2, downloadStartedEvents.Count());
        var lastEvent = downloadStartedEvents.OrderBy(recipientFileStatus => recipientFileStatus.RecipientFileStatusChanged).Last();
        Assert.Equal(lastEvent.RecipientFileStatusChanged, downloadedFileDetails.Recipients.FirstOrDefault(recipient => recipient.Recipient == lastEvent.Recipient)?.CurrentRecipientFileStatusChanged);
    }
}
