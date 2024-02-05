using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Application;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Hangfire.Common;
using Hangfire.States;

using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;
public class FileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly HttpClient _unregisteredClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public FileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
        _unregisteredClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_UNREGISTERED_TOKEN);
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
        var fileId = await InitializeAndAssertBasicFile();
        var fileAfterInitialize = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(fileAfterInitialize);
        Assert.True(fileAfterInitialize.FileStatus == FileStatusExt.Initialized);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
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

        // Attempt re-download
        var secondDownloadAttempt = await _recipientClient.GetAsync($"broker/api/v1/file/{fileId}/download");
        Assert.Equal(HttpStatusCode.Forbidden, secondDownloadAttempt.StatusCode);

        // Confirm that it has been enqueued for deletion
        _factory.HangfireBackgroundJobClient?.Verify(jobClient => jobClient.Create(
            It.Is<Job>(job => (job.Method.DeclaringType != null) && job.Method.DeclaringType.Name == "DeleteFileCommandHandler" && ((Guid)job.Args[0] == Guid.Parse(fileId))),
            It.IsAny<EnqueuedState>()));
    }

    [Fact]
    public async Task DownloadFile_WhenFileDownloadsTwice_ShowLastOccurenceInOverview()
    {
        // Arrange
        var fileId = await InitializeAndAssertBasicFile();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);
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

    [Fact]
    public async Task Search_SearchFileWith_From_To_Success()
    {
        // Arrange
        var fileId = await InitializeAndAssertBasicFile();
        DateTimeOffset dateTimeFrom = DateTime.Now.AddMinutes(-2);
        DateTimeOffset dateTimeTo = DateTime.Now.AddMinutes(2);
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/file?resourceId={initializedFile.ResourceId}&from={dateTimeFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&to={dateTimeTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileWith_From_To_Status_Success()
    {
        // Arrange
        string status = "Published";
        DateTimeOffset dateTimeFrom = DateTime.Now.AddMinutes(-2);
        DateTimeOffset dateTimeTo = DateTime.Now.AddMinutes(2);
        var fileId = await InitializeAndAssertBasicFile();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/file?resourceId={initializedFile.ResourceId}&from={dateTimeFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&to={dateTimeTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&status={status}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileWith_From_Status_Success()
    {
        // Arrange
        string status = "Published";
        DateTimeOffset dateTimeFrom = DateTime.Now.AddMinutes(-2);
        var fileId = await InitializeAndAssertBasicFile();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/file?resourceId={initializedFile.ResourceId}&from={dateTimeFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&status={status}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileWith_To_Status_Success()
    {
        // Arrange
        string status = "Published";
        DateTimeOffset dateTimeTo = DateTime.Now.AddMinutes(2);
        var fileId = await InitializeAndAssertBasicFile();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/file?resourceId={initializedFile.ResourceId}&to={dateTimeTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&status={status}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileWith_RecipientStatus_Success()
    {
        // Arrange
        string status = "Published";
        string recipientStatus = "Initialized";
        var fileId = await InitializeAndAssertBasicFile();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);

        // Act
        var searchResult = await _recipientClient.GetAsync($"broker/api/v1/file?resourceId={initializedFile.ResourceId}&status={status}&recipientStatus={recipientStatus}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileWith_RecipientStatus_NotFound()
    {
        // Arrange
        string status = "Published";
        string recipientStatus = "DownloadConfirmed";
        var fileId = await InitializeAndAssertBasicFile();
        var initializedFile = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(initializedFile);
        await UploadDummyFileAsync(fileId);

        // Act
        var searchResult = await _recipientClient.GetAsync($"broker/api/v1/file?status={status}&recipientStatus={recipientStatus}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain(fileId, contentstring);
    }

    [Fact]
    public async Task SendFile_UsingUnregisteredUser_Fails()
    {
        var initializeFileResponse = await _unregisteredClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.False(initializeFileResponse.IsSuccessStatusCode);
        var parsedError = await initializeFileResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.NoAccessToResource.Message, parsedError.Detail);
    }
    
    [Fact]
    public async Task UploadFile_ChecksumCorrect_Succeeds()
    {
        // Arrange
        var fileId = await InitializeAndAssertBasicFile();
        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var checksum = CalculateChecksum(fileContentBytes);
        var file = FileInitializeExtTestFactory.BasicFile();
        file.Checksum = checksum;

        // Act
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var uploadResponse = await UploadTextFile(fileId, fileContent);
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadFile_MismatchChecksum_Fails()
    {
        // Arrange
        var fileContent = "This is the contents of the uploaded file";
        var incorrectChecksumContent = "NOT THE CONTENTS OF UPLOADED FILE";
        var incorrectChecksumContentBytes = Encoding.UTF8.GetBytes(incorrectChecksumContent);
        var incorrectChecksum = CalculateChecksum(incorrectChecksumContentBytes);
        var file = FileInitializeExtTestFactory.BasicFile();
        file.Checksum = incorrectChecksum;

        // Act
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var uploadResponse = await UploadTextFile(fileId, fileContent);

        // Assert
        Assert.False(uploadResponse.IsSuccessStatusCode);
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadFile_NoChecksumSetWhenInitialized_ChecksumSetAfterUpload()
    {
        // Arrange
        var fileId = await InitializeAndAssertBasicFile();
        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var checksum = CalculateChecksum(fileContentBytes);

        // Act
        var uploadResponse = await UploadTextFile(fileId, fileContent);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());

        // Check if the checksum is set in the file details
        var fileDetails = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(fileDetails);
        Assert.NotNull(fileDetails.Checksum);
        Assert.Equal(checksum, fileDetails.Checksum);
    }

    [Fact]
    public async Task UploadFile_ChecksumSetWhenInitialized_SameChecksumSetAfterUpload()
    {
        // Arrange
        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var checksum = CalculateChecksum(fileContentBytes);
        var file = FileInitializeExtTestFactory.BasicFile();
        file.Checksum = checksum;

        // Act
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", file);
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        var fileId = await initializeFileResponse.Content.ReadAsStringAsync();
        var uploadResponse = await UploadTextFile(fileId, fileContent);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());

        // Check if the checksum is unchanged in the file details
        var fileDetails = await _senderClient.GetFromJsonAsync<FileOverviewExt>($"broker/api/v1/file/{fileId}", _responseSerializerOptions);
        Assert.NotNull(fileDetails);
        Assert.NotNull(fileDetails.Checksum);
        Assert.Equal(checksum, fileDetails.Checksum);
    }

    private async Task<HttpResponseMessage> UploadTextFile(string fileId, string fileContent)
    {
        var fileContents = Encoding.UTF8.GetBytes(fileContent);
        using (var content = new ByteArrayContent(fileContents))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            return uploadResponse;
        }
    }

    private async Task UploadDummyFileAsync(string fileId)
    {
        var fileContents = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(fileContents))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/file/{fileId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
    }

    private async Task<string> InitializeAndAssertBasicFile()
    {
        var initializeFileResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/file", FileInitializeExtTestFactory.BasicFile());
        Assert.True(initializeFileResponse.IsSuccessStatusCode, await initializeFileResponse.Content.ReadAsStringAsync());
        return await initializeFileResponse.Content.ReadAsStringAsync();
    }

    private string CalculateChecksum(byte[] data)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
