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
        _senderClient = factory.CreateClient();
        _senderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_SENDER_TOKEN);
        _legacyClient = factory.CreateClient();
        _legacyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestConstants.DUMMY_LEGACY_TOKEN);

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
        var initializeFileResponse = await _legacyClient.PostAsJsonAsync("broker/api/legacy/v1/file", FileInitializeExtTestFactory.BasicFile());
        string onBehalfOfConsumer = FileInitializeExtTestFactory.BasicFile().Sender;
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
        ActorFileStatus status = ActorFileStatus.Initialized;
        string serviceReference = "resource-2";
        string recipient1 = FileInitializeExtTestFactory.BasicFile_MultipleRecipients().Recipients[0];
        string recipient2 = FileInitializeExtTestFactory.BasicFile_MultipleRecipients().Recipients[1];
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile_MultipleRecipients());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={serviceReference}"
        + $"&recipients={recipient1}"
        + $"&recipients={recipient2}");

        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFiles_GetByMultipleRecipient_Recipient1_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileStatus status = ActorFileStatus.Initialized;
        string serviceReference = "resource-2";
        string recipient1 = FileInitializeExtTestFactory.BasicFile_MultipleRecipients().Recipients[0];
        string recipient2 = FileInitializeExtTestFactory.BasicFile_MultipleRecipients().Recipients[1];
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile_MultipleRecipients());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={serviceReference}"
        + $"&recipients={recipient1}");

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
        ActorFileStatus status = ActorFileStatus.Initialized;
        string serviceReference = "resource-2";
        string recipient1 = FileInitializeExtTestFactory.BasicFile_MultipleRecipients().Recipients[0];
        string recipient2 = FileInitializeExtTestFactory.BasicFile_MultipleRecipients().Recipients[1];
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile_MultipleRecipients());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={serviceReference}"
        + $"&recipients={recipient2}");

        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFiles_GetBySingleRecipient_Success()
    {
        // Arrange
        DateTimeOffset from = DateTimeOffset.Now.AddHours(-1);
        DateTimeOffset to = DateTimeOffset.Now.AddHours(1);
        ActorFileStatus status = ActorFileStatus.Initialized;
        string serviceReference = "resource-1";
        string onBehalfOfConsumer = FileInitializeExtTestFactory.BasicFile().Recipients[0];
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file?recipientStatus={status}"
        + $"&from={HttpUtility.UrlEncode(from.UtcDateTime.ToString("o"))}&to={HttpUtility.UrlEncode(to.UtcDateTime.ToString("o"))}"
        + $"&resourceId={serviceReference}"
        + $"&onBehalfOfConsumer={onBehalfOfConsumer}");

        var result = await getResponse.Content.ReadAsAsync<List<Guid>>();

        // Assert        
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Guid.Parse(fileId), result);
    }

    [Fact]
    public async Task GetFileOverview_SentByA3Sender_Success()
    {
        // Arrange
        string onBehalfOfConsumer = FileInitializeExtTestFactory.BasicFile().Recipients[0];
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");
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
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetFileOverview_FileDoesNotExist_FileNotFound()
    {
        // Arrange
        string onBehalfOfConsumer = FileInitializeExtTestFactory.BasicFile().Recipients[0];
        string fileId = "00000000-0000-0000-0000-000000000000";

        // Act
        var getResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
    }
    
    [Fact]
    public async Task Download_DownloadFile_Success()
    {
        // Arrange
        string onBehalfOfConsumer = FileInitializeExtTestFactory.BasicFile().Recipients[0];

        // Arrange - initialize file
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();

        // Arrange - upload file
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var downloadedFile = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}/download?onBehalfOfConsumer={onBehalfOfConsumer}");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);
    }
    
    [Fact]
    public async Task Download_ConfirmDownloaded_Success()
    {
        // Arrange
        string onBehalfOfConsumer = FileInitializeExtTestFactory.BasicFile().Recipients[0];
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }

        // Act
        var getResponse = await _legacyClient.PostAsync($"broker/api/legacy/v1/file/{fileId}/confirmdownload?onBehalfOfConsumer={onBehalfOfConsumer}", null);
        var statusResponse = await _legacyClient.GetAsync($"broker/api/legacy/v1/file/{fileId}?onBehalfOfConsumer={onBehalfOfConsumer}");
        var result = await statusResponse.Content.ReadAsAsync<LegacyFileOverviewExt>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(LegacyRecipientFileStatusExt.DownloadConfirmed, result?.Recipients[0]?.CurrentRecipientFileStatusCode);
        Assert.Equal(LegacyFileStatusExt.AllConfirmedDownloaded, result?.FileStatus);
    }
}
