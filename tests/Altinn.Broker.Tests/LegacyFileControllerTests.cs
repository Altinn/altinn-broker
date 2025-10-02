using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;

using Altinn.Broker.API.Models;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
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
    private readonly HttpClient _recipientClient;
    private readonly HttpClient _serviceOwnerClient;
    private readonly HttpClient _legacyClient;
    private readonly HttpClient _webhookClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public LegacyFileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
        _legacyClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_LEGACY_TOKEN);
        _serviceOwnerClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _webhookClient = factory.CreateClient();
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
        var initializeFileResponse = await _legacyClient.PostAsJsonAsync("broker/api/v1/legacy/file", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        string onBehalfOfConsumer = FileTransferInitializeExtTestFactory.BasicFileTransfer().Sender;
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var fileAfterInitialize = await _legacyClient.GetFromJsonAsync<LegacyFileOverviewExt>($"broker/api/v1/legacy/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}", _responseSerializerOptions);
        Assert.NotNull(fileAfterInitialize);
        Assert.Equal(LegacyFileStatusExt.Initialized, fileAfterInitialize.FileStatus);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _legacyClient.PostAsync($"broker/api/v1/legacy/file/{fileId}/upload?onBehalfOfConsumer={onBehalfOfConsumer}", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
        var fileAfterUpload = await _legacyClient.GetFromJsonAsync<LegacyFileOverviewExt>($"broker/api/v1/legacy/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}", _responseSerializerOptions);
        Assert.NotNull(fileAfterUpload);
        Assert.True(fileAfterUpload.FileSize == uploadedFileBytes.Length);
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
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&recipients={file.Recipients[0]}"
        + $"&recipients={file.Recipients[1]}");

        var textResponse = await getResponse.Content.ReadAsStringAsync();

        var result = await getResponse.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(result);
        Assert.Contains(Guid.Parse(fileTransferId), result);
    }

    [Fact]
    public async Task InitializeAndUpload_FailsDueToAntivirus()
    {
        // Initialize
        var initializeFileResponse = await _legacyClient.PostAsJsonAsync("broker/api/v1/legacy/file", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        string onBehalfOfConsumer = FileTransferInitializeExtTestFactory.BasicFileTransfer().Sender;
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var fileAfterInitialize = await _legacyClient.GetFromJsonAsync<LegacyFileOverviewExt>($"broker/api/v1/legacy/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}", _responseSerializerOptions);
        Assert.NotNull(fileAfterInitialize);
        Assert.Equal(LegacyFileStatusExt.Initialized, fileAfterInitialize.FileStatus);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _legacyClient.PostAsync($"broker/api/v1/legacy/file/{fileId}/upload?onBehalfOfConsumer={onBehalfOfConsumer}", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        var jsonBody = GetMalwareScanResultJson("Data/MalwareScanResult_Malicious.json", fileId);
        await SendMalwareScanResult(jsonBody);

        var fileAfterUpload = await _legacyClient.GetFromJsonAsync<LegacyFileOverviewExt>($"broker/api/v1/legacy/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}", _responseSerializerOptions);
        Assert.NotNull(fileAfterUpload);
        Assert.Equal(LegacyFileStatusExt.Failed, fileAfterUpload.FileStatus);
        Assert.StartsWith("Malware", fileAfterUpload.FileStatusText);
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

        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);        
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&recipients={file.Recipients[0]}");

        var result = await getResponse.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(result);
        Assert.Contains(Guid.Parse(fileTransferId), result);
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
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&recipients={file.Recipients[1]}");

        var result = await getResponse.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(result);
        Assert.Contains(Guid.Parse(fileTransferId), result);
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
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        var result = await getResponse.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileTransferId), result!);
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
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        var downloadedFile = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}/download?onBehalfOfConsumer={file.Recipients[1]}");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();

        // Act
        var getResponse_rep1 = await _legacyClient.GetAsync($"broker/api/v1/legacy/file?status=Published&recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        var getResponse_rep2 = await _legacyClient.GetAsync($"broker/api/v1/legacy/file?status=Published&recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[1]}");

        var result_recip1 = await getResponse_rep1.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);
        var result_recip2 = await getResponse_rep2.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse_rep1.StatusCode);
        Assert.Contains(Guid.Parse(fileTransferId), result_recip1!);
        Assert.Contains(Guid.Parse(fileTransferId), result_recip2!);
    }

    [Fact]
    public async Task GetFileOverview_SentByA3Sender_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}?onBehalfOfConsumer={file.Recipients[0]}");
        var fileData = await getResponse.Content.ReadFromJsonAsync<LegacyFileOverviewExt>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fileData);
        Assert.Equal(fileTransferId, fileData.FileId.ToString());
    }

    [Fact]
    public async Task GetFileOverview_MultipleSentByA3Sender_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var initializeFileResponse2 = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse2.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var initializeFileResponse3 = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse3.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var initializeFileResponse4 = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse4.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferResponse2 = await initializeFileResponse2.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferResponse3 = await initializeFileResponse3.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferResponse4 = await initializeFileResponse4.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();        
        Assert.NotNull(fileTransferResponse);
        Assert.NotNull(fileTransferResponse2);
        Assert.NotNull(fileTransferResponse3);
        Assert.NotNull(fileTransferResponse4);
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var fileTransferId2 = fileTransferResponse2.FileTransferId.ToString();
        var fileTransferId3 = fileTransferResponse3.FileTransferId.ToString();
        var fileTransferId4 = fileTransferResponse4.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        using (var content2 = new ByteArrayContent(uploadedFileBytes))
        using (var content3 = new ByteArrayContent(uploadedFileBytes))
        using (var content4 = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content2.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content3.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content4.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            var uploadResponse2 = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId2}/upload", content2);
            var uploadResponse3 = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId3}/upload", content3);
            var uploadResponse4 = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId4}/upload", content4);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
            Assert.True(uploadResponse2.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
            Assert.True(uploadResponse3.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
            Assert.True(uploadResponse4.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        List<Guid> ids = [fileTransferResponse.FileTransferId, fileTransferResponse2.FileTransferId, fileTransferResponse3.FileTransferId, fileTransferResponse4.FileTransferId];

        // Act
        var getResponse = await _legacyClient.PostAsJsonAsync($"broker/api/v1/legacy/file/overviews?onBehalfOfConsumer={file.Recipients[0]}", ids);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fileData = await getResponse.Content.ReadFromJsonAsync<List<LegacyFileOverviewExt>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fileData);
        var resultIds = fileData.Select(fd => fd.FileId.ToString());
        Assert.Contains(fileTransferId, resultIds);
        Assert.Contains(fileTransferId2, resultIds);
        Assert.Contains(fileTransferId3, resultIds);
        Assert.Contains(fileTransferId4, resultIds);
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
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file"
        + $"?status=Published"
        + $"&recipientStatus=Initialized"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        string s = await getResponse.Content.ReadAsStringAsync();
        var fileData = await getResponse.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fileData);
        Assert.Contains(fileData, g => g == Guid.Parse(fileId1));
        Assert.DoesNotContain(fileData, g => g == Guid.Parse(fileId2));
    }

    [Fact]
    public async Task GetFileOverview_3Published_GetInOrder_Success()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        string fileId1 = await InitializeFile();
        await UploadFile(fileId1);
        string fileId2 = await InitializeFile();
        await UploadFile(fileId2);
        string fileId3 = await InitializeFile();
        await UploadFile(fileId3);

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file"
        + $"?status=Published"
        + $"&recipientStatus=Initialized"
        + $"&resourceId={file.ResourceId}"
        + $"&onBehalfOfConsumer={file.Recipients[0]}");
        string s = await getResponse.Content.ReadAsStringAsync();
        var fileData = await getResponse.Content.ReadFromJsonAsync<List<Guid>>(_responseSerializerOptions);

        // Assert        
        int fileIndex1, fileIndex2, fileIndex3 = -1;
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fileData);
        fileIndex1 = fileData.FindIndex(g => g == Guid.Parse(fileId1));
        fileIndex2 = fileData.FindIndex(g => g == Guid.Parse(fileId2));
        fileIndex3 = fileData.FindIndex(g => g == Guid.Parse(fileId3));
        Assert.NotEqual(-1, fileIndex1);
        Assert.NotEqual(-1, fileIndex2);
        Assert.NotEqual(-1, fileIndex3);
        Assert.True(fileIndex1 < fileIndex2 && fileIndex2 < fileIndex3);
    }

    [Fact]
    public async Task GetFileOverview_FileDoesNotExist_FileNotFound()
    {
        // Arrange
        string onBehalfOfConsumer = FileTransferInitializeExtTestFactory.BasicFileTransfer().Recipients[0];
        string fileId = "00000000-0000-0000-0000-000000000000";

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");

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
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        

        // Arrange - upload file
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var downloadedFile = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}/download?onBehalfOfConsumer={file.Recipients[0]}");
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
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferAfterAllRecipientsConfirmed = false
        });
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
        var downloadedFile = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}/download?onBehalfOfConsumer={file.Recipients[0]}");
        Assert.True(downloadedFile.IsSuccessStatusCode);

        // Act
        var getResponse = await _legacyClient.PostAsync($"broker/api/v1/legacy/file/{fileTransferId}/confirmdownload?onBehalfOfConsumer={file.Recipients[0]}", null);
        var statusResponse = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}?onBehalfOfConsumer={file.Recipients[0]}");
        var result = await statusResponse.Content.ReadFromJsonAsync<LegacyFileOverviewExt>(_responseSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(LegacyRecipientFileStatusExt.DownloadConfirmed, result?.Recipients[0]?.CurrentRecipientFileStatusCode);
        Assert.Equal(LegacyFileStatusExt.AllConfirmedDownloaded, result?.FileStatus);
    }

    [Fact]
    public async Task DownloadZipFile_WithManifestShimEnabled_ManifestIsAdded()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer_ManifestShim();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var uploadedFileLength = await UploadZipFile(fileTransferId);

        // Act
        var downloadedFile = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}/download?onBehalfOfConsumer={file.Recipients[0]}");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.True(downloadedFileBytes.Length > uploadedFileLength);
        var manifestDownloadStream = new ManifestDownloadStream(downloadedFileBytes);
        var addedManifest = manifestDownloadStream.GetBrokerManifest();

        // Assert
        Assert.NotNull(addedManifest);
        Assert.Equal("991825827", addedManifest.Reportee);
        Assert.Equal(addedManifest.SendersReference, file.SendersFileTransferReference);
        Assert.True(addedManifest.PropertyList.All(property => file.PropertyList[property.PropertyKey] == property.PropertyValue));
    }

    [Fact]
    public async Task DownloadZipFile_WithoutManifestShimEnabled_ManifestIsNotAdded()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var uploadedFileLength = await UploadZipFile(fileTransferId);

        // Act
        var downloadedFile = await _legacyClient.GetAsync($"broker/api/v1/legacy/file/{fileTransferId}/download?onBehalfOfConsumer={file.Recipients[0]}");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(downloadedFileBytes.Length, uploadedFileLength);
        var manifestDownloadStream = new ManifestDownloadStream(downloadedFileBytes);
        var addedManifest = manifestDownloadStream.GetBrokerManifest();

        // Assert
        Assert.Null(addedManifest);
    }

    [Fact]
    public async Task ZipFile_WithManifestShimEnabled_NoManifestWithNonLegacyApi()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer_ManifestShim();
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var uploadedFileLength = await UploadZipFile(fileTransferId);

        // Act
        var downloadedFile = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(uploadedFileLength, downloadedFileBytes.LongLength);
        var manifestDownloadStream = new ManifestDownloadStream(downloadedFileBytes);
        var addedManifest = manifestDownloadStream.GetBrokerManifest();

        // Assert
        Assert.Null(addedManifest);
    }

    private string GetMalwareScanResultJson(string filePath, string fileId)
    {
        string jsonBody = File.ReadAllText(filePath);
        var randomizedEtagId = Guid.NewGuid().ToString();
        jsonBody = jsonBody.Replace("--FILEID--", fileId);
        jsonBody = jsonBody.Replace("--ETAGID--", randomizedEtagId.ToString());
        return jsonBody;
    }
    private async Task<HttpResponseMessage> SendMalwareScanResult(string jsonBody)
    {
        var result = await _webhookClient.PostAsync("broker/api/v1/webhooks/malwarescanresults", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
        Assert.True(result.IsSuccessStatusCode, $"The request failed with status code {result.StatusCode}. Error message: {await result.Content.ReadAsStringAsync()}");
        return result;
    }

    private async Task<string> InitializeFile()
    {
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);

        return fileTransferId;
    }

    private async Task UploadFile(string fileTransferId)
    {
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
    }

    private async Task<long> UploadZipFile(string fileTransferId) {
        var payloadFile = await GenerateDummyFile(10240);
        using var zipStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var zippedFile = zipArchive.CreateEntry("payload.txt");
            using (var zippedFileStream = zippedFile.Open())
            {
                await zippedFileStream.WriteAsync(payloadFile);
            }
        }
        zipStream.Position = 0;
        var zipBytes = zipStream.ToArray();
        using (var content = new ByteArrayContent(zipStream.ToArray()))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
        return zipStream.Length;
    }

    private async Task<byte[]> GenerateDummyFile(long bytes)
    {
        var fileBuffer = new byte[bytes];
        new Random().NextBytes(fileBuffer);
        return fileBuffer;
    }
}
