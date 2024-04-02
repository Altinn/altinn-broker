using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;

using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;
public class LegacyFileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _legacyClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public LegacyFileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _legacyClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_LEGACY_TOKEN);
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeAndUpload_Success()
    {
        // Initialize
        var initializeFileResponse = await _legacyClient.PostAsJsonAsync("broker/api/legacy/v1/file", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        string onBehalfOfConsumer = FileTransferInitializeExtTestFactory.BasicFileTransfer().Sender;
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var fileAfterInitialize = await _legacyClient.GetFromJsonAsync<LegacyFileOverviewExt>($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}", _responseSerializerOptions);
        Assert.NotNull(fileAfterInitialize);
        Assert.Equal(LegacyFileStatusExt.Initialized, fileAfterInitialize.FileStatus);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _legacyClient.PostAsync($"broker/api/legacy/v1/file/{fileId}/upload?onBehalfOfConsumer={onBehalfOfConsumer}", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
        var fileAfterUpload = await _legacyClient.GetFromJsonAsync<LegacyFileOverviewExt>($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}", _responseSerializerOptions);
        Assert.NotNull(fileAfterUpload);
        Assert.Equal(LegacyFileStatusExt.Published, fileAfterUpload.FileStatus); // When running integration test this happens instantly as of now.
    }

    [Fact]
    public async Task GetFiles_GetByMultipleRecipient_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileTransferStatus status = ActorFileTransferStatus.Initialized;
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer_MultipleRecipients();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&recipients={file.Recipients[0]}"
        + $"&recipients={file.Recipients[1]}");

        var textResponse = await getResponse.Content.ReadAsStringAsync();

        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFiles_GetByMultipleRecipient_Recipient1_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileTransferStatus status = ActorFileTransferStatus.Initialized;
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer_MultipleRecipients();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&recipients={file.Recipients[0]}");

        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFiles_GetByMultipleRecipient_Recipient2_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileTransferStatus status = ActorFileTransferStatus.Initialized;
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer_MultipleRecipients();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&recipients={file.Recipients[1]}");

        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFiles_GetBySingleRecipient_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileTransferStatus status = ActorFileTransferStatus.Initialized;
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFiles_GetInitializedAndDownloadStarted_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileTransferStatus status = ActorFileTransferStatus.Initialized;
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer_MultipleRecipients();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        var downloadedFile = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}/download?onBehalfOfConsumer={file.Recipients[1]}");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();

        // Act
        var getResponse_rep1 = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?status=Published&recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        var getResponse_rep2 = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?status=Published&recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[1]}");

        var result_recip1 = await getResponse_rep1.Content.ReadAsAsync<List<Guid>>();
        var result_recip2 = await getResponse_rep2.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse_rep1.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result_recip1);
        Assert.Contains(Guid.Parse(fileId), result_recip2);
    }

    [Fact]
    public async Task GetFileOverview_SentByA3Sender_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={file.Recipients[0]}");
        var fileData = await getResponse.Content.ReadAsAsync<LegacyFileOverviewExt>();

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(fileId, fileData.FileId.ToString());
    }

    [Fact]
    public async Task GetFileOverview_ConsumerIsNotPartOfFile_FileNotFound()
    {
        // Arrange
        string onBehalfOfConsumer = "0199:999999999";
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetFileOverview_2FilesInitiated_1Published_StandardRequestRetrievesOnlyPublished_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        string fileId1 = await InitializeFile();
        await UploadFile(fileId1);
        string fileId2 = await InitializeFile();

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file"
        + $"?status=Published"
        + $"&recipientStatus=Initialized"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        string s = await getResponse.Content.ReadAsStringAsync();
        List<Guid> fileData = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(fileData, g => g == Guid.Parse(fileId1));
        Assert.DoesNotContain(fileData, g => g == Guid.Parse(fileId2));
    }

    [Fact]
    public async Task GetFileOverview_FileDoesNotExist_FileNotFound()
    {
        // Arrange
        string onBehalfOfConsumer = FileTransferInitializeExtTestFactory.BasicFileTransfer().Recipients[0];
        string fileId = "00000000-0000-0000-0000-000000000000";

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Download_DownloadFile_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();

        // Arrange - initialize file
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();

        // Arrange - upload file
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var downloadedFile = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}/download?onBehalfOfConsumer={file.Recipients[0]}");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.True(downloadedFileBytes?.Length > 0);
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);
    }

    [Fact]
    public async Task Download_ConfirmDownloaded_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.PostAsync($"broker/api/legacy/v1/file/{fileId}/confirmdownload?onBehalfOfConsumer={file.Recipients[0]}", null);
        var statusResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={file.Recipients[0]}");
        var result = await statusResponse.Content.ReadAsAsync<LegacyFileOverviewExt>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(LegacyRecipientFileStatusExt.DownloadConfirmed, result?.Recipients[0]?.CurrentRecipientFileStatusCode);
        Assert.Equal(LegacyFileStatusExt.AllConfirmedDownloaded, result?.FileStatus);
    }

    private async Task<string> InitializeFile()
    {
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);

        return fileId;
    }

    private async Task UploadFile(string fileId)
    {
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
    }
}
