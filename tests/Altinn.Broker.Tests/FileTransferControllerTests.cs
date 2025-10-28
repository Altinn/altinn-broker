using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml;

using Altinn.Broker.API.Models;
using Altinn.Broker.Application;
using Altinn.Broker.Application.PurgeFileTransfer;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Hangfire;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Abstractions;

namespace Altinn.Broker.Tests;
public class FileTransferControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly HttpClient _serviceOwnerClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;
    private readonly ITestOutputHelper _output;

    public FileTransferControllerTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
        _serviceOwnerClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _output = output;
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task Search_WithRoleRecipient_ExcludesSenderOnly()
    {
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var overview = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(overview);
        await UploadDummyFileTransferAsync(fileTransferId);

        var asSender = await _senderClient.GetAsync($"broker/api/v1/filetransfer?resourceId={overview.ResourceId}&status=Published&role=recipient");
        var senderContent = await asSender.Content.ReadAsStringAsync();
        Assert.DoesNotContain(fileTransferId, senderContent); // sender querying recipient-only should not see own sent file

        var asRecipient = await _recipientClient.GetAsync($"broker/api/v1/filetransfer?resourceId={overview.ResourceId}&status=Published&role=recipient");
        var recipientContent = await asRecipient.Content.ReadAsStringAsync();
        Assert.Contains(fileTransferId, recipientContent);
    }

    [Fact]
    public async Task Search_WithRoleSender_ExcludesRecipientOnly()
    {
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var overview = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(overview);
        await UploadDummyFileTransferAsync(fileTransferId);

        var asSender = await _senderClient.GetAsync($"broker/api/v1/filetransfer?resourceId={overview.ResourceId}&status=Published&role=sender");
        var senderContent = await asSender.Content.ReadAsStringAsync();
        Assert.Contains(fileTransferId, senderContent);

        var asRecipient = await _recipientClient.GetAsync($"broker/api/v1/filetransfer?resourceId={overview.ResourceId}&status=Published&role=sender");
        var recipientContent = await asRecipient.Content.ReadAsStringAsync();
        Assert.DoesNotContain(fileTransferId, recipientContent);
    }

    [Fact]
    public async Task NormalFlow_WhenAllIsOK_Success()
    {
        // Initialize
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var fileTransferAfterInitialize = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferAfterInitialize);
        Assert.True(fileTransferAfterInitialize.FileTransferStatus == FileTransferStatusExt.Initialized);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
        var fileTransferAfterUpload = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferAfterUpload);
        Assert.True(fileTransferAfterUpload.FileTransferStatus == FileTransferStatusExt.Published); // When running integration test this happens instantly as of now.

        // Download
        var downloadedFile = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);

        // Details
        var downloadedFileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileTransferDetails);
        Assert.True(downloadedFileTransferDetails.FileTransferStatus == FileTransferStatusExt.Published);
        Assert.Contains(downloadedFileTransferDetails.RecipientFileTransferStatusHistory, recipient => recipient.RecipientFileTransferStatusCode == RecipientFileTransferStatusExt.DownloadStarted);

        // Confirm
        await _recipientClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/confirmdownload", null);
        var confirmedFileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(confirmedFileTransferDetails);
        Assert.True(confirmedFileTransferDetails.FileTransferStatus == FileTransferStatusExt.AllConfirmedDownloaded);
        Assert.Contains(confirmedFileTransferDetails.RecipientFileTransferStatusHistory, recipient => recipient.RecipientFileTransferStatusCode == RecipientFileTransferStatusExt.DownloadConfirmed);

        // Attempt re-download
        var secondDownloadAttempt = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.Equal(HttpStatusCode.OK, secondDownloadAttempt.StatusCode);
    }

    [Fact]
    public async Task NormalFlow_With10Properties_Success()
    {
        // Initialize
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.PropertyList.Add("SuperProperty01", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty02", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty03", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty04", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty05", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty06", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty07", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty08", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty09", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty10", "BLAHBLAHBLAH");

        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId;

        var fileTransferAfterInitialize = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferAfterInitialize);
        Assert.True(fileTransferAfterInitialize.FileTransferStatus == FileTransferStatusExt.Initialized);

        // Upload
        var uploadedFileBytes = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(uploadedFileBytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
        var fileTransferAfterUpload = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferAfterUpload);
        Assert.True(fileTransferAfterUpload.FileTransferStatus == FileTransferStatusExt.Published); // When running integration test this happens instantly as of now.

        // Download
        var downloadedFile = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        Assert.Equal(uploadedFileBytes, downloadedFileBytes);

        // Details
        var downloadedFileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileTransferDetails);
        Assert.True(downloadedFileTransferDetails.FileTransferStatus == FileTransferStatusExt.Published);
        Assert.Contains(downloadedFileTransferDetails.RecipientFileTransferStatusHistory, recipient => recipient.RecipientFileTransferStatusCode == RecipientFileTransferStatusExt.DownloadStarted);

        // Confirm
        await _recipientClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/confirmdownload", null);
        var confirmedFileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(confirmedFileTransferDetails);
        Assert.True(confirmedFileTransferDetails.FileTransferStatus == FileTransferStatusExt.AllConfirmedDownloaded);
        Assert.Contains(confirmedFileTransferDetails.RecipientFileTransferStatusHistory, recipient => recipient.RecipientFileTransferStatusCode == RecipientFileTransferStatusExt.DownloadConfirmed);

        // Attempt re-download
        var secondDownloadAttempt = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.Equal(HttpStatusCode.OK, secondDownloadAttempt.StatusCode);
    }

    [Fact]
    public async Task Initialize_With11Properties_ValidationFailure()
    {
        // Initialize
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.PropertyList.Add("SuperProperty01", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty02", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty03", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty04", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty05", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty06", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty07", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty08", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty09", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty10", "BLAHBLAHBLAH");
        initializeRequestBody.PropertyList.Add("SuperProperty11", "BLAHBLAHBLAH");

        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);

        Assert.False(initializeFileTransferResponse.IsSuccessStatusCode);
        var parsedError = await initializeFileTransferResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Contains("PropertyList can contain at most 10 properties", parsedError.Extensions.First().Value.ToString());
    }

    [Fact]
    public async Task Initialize_WitPropertiesInvalidKeyLength_ValidationFailure()
    {
        // Initialize
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.PropertyList.Add("yesthispropertykyelengthismuchmorethan50actuallyitis54", "actuallyAValidValue");

        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);

        Assert.False(initializeFileTransferResponse.IsSuccessStatusCode);
        var parsedError = await initializeFileTransferResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Contains("PropertyList Key can not be longer than 50", parsedError.Extensions.First().Value.ToString());
    }

    [Fact]
    public async Task Initialize_WitPropertiesInvalidValueLength_ValidationFailure()
    {
        // Initialize
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.PropertyList.Add("actuallyAValidKey", "thisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvaluethisisanextremelylongvalue315");

        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);

        Assert.False(initializeFileTransferResponse.IsSuccessStatusCode);
        var parsedError = await initializeFileTransferResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Contains("PropertyList Value can not be longer than 300", parsedError.Extensions.First().Value.ToString());
    }

    [Fact]
    public async Task DownloadFileTransfer_WhenFileDownloadsTwice_ShowLastOccurenceInOverview()
    {
        // Arrange
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);
        var uploadedFile = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);

        // Act
        var downloadedFile1 = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        await Task.Delay(1000);
        var downloadedFile2 = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        var downloadedFile1Bytes = await downloadedFile1.Content.ReadAsByteArrayAsync();
        var downloadedFile2Bytes = await downloadedFile2.Content.ReadAsByteArrayAsync();
        Assert.Equal(downloadedFile1Bytes, downloadedFile2Bytes);


        // Assert
        var downloadedFileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(downloadedFileTransferDetails);
        Assert.Contains(downloadedFileTransferDetails.RecipientFileTransferStatusHistory, recipient => recipient.RecipientFileTransferStatusCode == RecipientFileTransferStatusExt.DownloadStarted);
        var downloadStartedEvents = downloadedFileTransferDetails.RecipientFileTransferStatusHistory.Where(recipientFileStatus => recipientFileStatus.RecipientFileTransferStatusCode == RecipientFileTransferStatusExt.DownloadStarted);
        Assert.NotNull(downloadStartedEvents);
        Assert.Equal(2, downloadStartedEvents.Count());
        var lastEvent = downloadStartedEvents.OrderBy(recipientFileTransferStatus => recipientFileTransferStatus.RecipientFileTransferStatusChanged).Last();
        Assert.Equal(lastEvent.RecipientFileTransferStatusChanged, downloadedFileTransferDetails.Recipients.FirstOrDefault(recipient => recipient.Recipient == lastEvent.Recipient)?.CurrentRecipientFileTransferStatusChanged);
    }

    [Fact]
    public async Task ConfirmFileTransferBeforeDownloadStarted_Fails()
    {
        // Arrange
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act
        var confirmResponse = await _recipientClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/confirmdownload", null);

        // Assert
        Assert.False(confirmResponse.IsSuccessStatusCode);
        var parsedError = await confirmResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.ConfirmDownloadBeforeDownloadStarted.Message, parsedError.Detail);
    }

    [Fact]
    public async Task Search_SearchFileTransferWith_From_To_Success()
    {
        // Arrange
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        DateTimeOffset dateTimeFrom = DateTime.Now.AddMinutes(-2);
        DateTimeOffset dateTimeTo = DateTime.Now.AddMinutes(2);
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/filetransfer?resourceId={initializedFileTransfer.ResourceId}&from={dateTimeFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&to={dateTimeTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileTransferId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileTransferWith_From_To_Status_Success()
    {
        // Arrange
        string status = "Published";
        DateTimeOffset dateTimeFrom = DateTime.Now.AddMinutes(-2);
        DateTimeOffset dateTimeTo = DateTime.Now.AddMinutes(2);
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/filetransfer?resourceId={initializedFileTransfer.ResourceId}&from={dateTimeFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&to={dateTimeTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&status={status}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileTransferId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileTransferWith_From_Status_Success()
    {
        // Arrange
        string status = "Published";
        DateTimeOffset dateTimeFrom = DateTime.Now.AddMinutes(-2);
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/filetransfer?resourceId={initializedFileTransfer.ResourceId}&from={dateTimeFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&status={status}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();
        // Assert
        Assert.Contains(fileTransferId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileTransferWith_To_Status_Success()
    {
        // Arrange
        string status = "Published";
        DateTimeOffset dateTimeTo = DateTime.Now.AddMinutes(2);
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act        
        var searchResult = await _senderClient.GetAsync($"broker/api/v1/filetransfer?resourceId={initializedFileTransfer.ResourceId}&to={dateTimeTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture.DateTimeFormat)}&status={status}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileTransferId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileTransferWith_RecipientStatus_Success()
    {
        // Arrange
        string status = "Published";
        string recipientStatus = "Initialized";
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act
        var searchResult = await _recipientClient.GetAsync($"broker/api/v1/filetransfer?resourceId={initializedFileTransfer.ResourceId}&status={status}&recipientStatus={recipientStatus}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(fileTransferId, contentstring);
    }

    [Fact]
    public async Task Search_SearchFileTransferWith_RecipientStatus_NotFound()
    {
        // Arrange
        string status = "Published";
        string recipientStatus = "DownloadConfirmed";
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var initializedFileTransfer = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(initializedFileTransfer);
        await UploadDummyFileTransferAsync(fileTransferId);

        // Act
        var searchResult = await _recipientClient.GetAsync($"broker/api/v1/filetransfer?status={status}&recipientStatus={recipientStatus}");
        string contentstring = await searchResult.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain(fileTransferId, contentstring);
    }

    [Fact]
    public async Task UploadFileTransfer_ChecksumCorrect_Succeeds()
    {
        // Arrange
        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var checksum = CalculateChecksum(fileContentBytes);
        var fileTransfer = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        fileTransfer.Checksum = checksum;

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", fileTransfer);
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, fileTransferId);
        var uploadResponse = await UploadTextFileTransfer(fileTransferId, fileContent);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        var fileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferDetails);
        Assert.NotNull(fileTransferDetails.Checksum);
        Assert.Equal(checksum, fileTransferDetails.Checksum);
    }

    [Fact]
    public async Task UploadFileTransfer_MismatchChecksum_Fails()
    {
        // Arrange
        var fileContent = "This is the contents of the uploaded file";
        var incorrectChecksumContent = "NOT THE CONTENTS OF UPLOADED FILE";
        var incorrectChecksumContentBytes = Encoding.UTF8.GetBytes(incorrectChecksumContent);
        var incorrectChecksum = CalculateChecksum(incorrectChecksumContentBytes);
        var fileTransfer = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        fileTransfer.Checksum = incorrectChecksum;

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", fileTransfer);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferId = await initializeFileTransferResponse.Content.ReadAsStringAsync();
        var uploadResponse = await UploadTextFileTransfer(fileTransferId, fileContent);

        // Assert
        Assert.False(uploadResponse.IsSuccessStatusCode);
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadFileTransfer_NoChecksumSetWhenInitialized_ChecksumSetAfterUpload()
    {
        // Arrange
        var fileTransferId = await InitializeAndAssertBasicFileTransfer();
        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var checksum = CalculateChecksum(fileContentBytes);

        // Act
        var uploadResponse = await UploadTextFileTransfer(fileTransferId, fileContent);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());

        // Check if the checksum is set in the file details
        var fileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferDetails);
        Assert.NotNull(fileTransferDetails.Checksum);
        Assert.Equal(checksum, fileTransferDetails.Checksum);
    }

    [Fact]
    public async Task UploadFileTransfer_ChecksumSetWhenInitialized_SameChecksumSetAfterUpload()
    {
        // Arrange
        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var checksum = CalculateChecksum(fileContentBytes);
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        file.Checksum = checksum;

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var uploadResponse = await UploadTextFileTransfer(fileTransferId, fileContent);

        // Assert
        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());

        // Check if the checksum is unchanged in the file details
        var fileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferDetails);
        Assert.NotNull(fileTransferDetails.Checksum);
        Assert.Equal(checksum, fileTransferDetails.Checksum);
    }

    [Fact]
    public async Task SendFileTransfer_UserWithoutAccess_Fails()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        file.ResourceId = TestConstants.RESOURCE_WITH_NO_ACCESS;

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);

        // Assert
        Assert.False(initializeFileTransferResponse.IsSuccessStatusCode);
        var parsedError = await initializeFileTransferResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.NoAccessToResource.Message, parsedError.Detail);
    }

    [Fact]
    public async Task SendFileTransfer_ResourceWithBlankServiceOwner_Fails()
    {
        // Arrange
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        file.ResourceId = TestConstants.RESOURCE_WITH_NO_SERVICE_OWNER;

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);

        // Assert
        Assert.False(initializeFileTransferResponse.IsSuccessStatusCode);
        var parsedError = await initializeFileTransferResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.InvalidResourceDefinition.Message, parsedError.Detail);
    }

    [Fact]
    public async Task Graceful_purge_changes_purge_time()
    {
        await Test_Graceful_purge_changes_purge_time("PT12H");
        await Test_Graceful_purge_changes_purge_time("PT1H");
        await Test_Graceful_purge_changes_purge_time("PT1M");
    }

    [Fact]
    public async Task InitializeFileTransfer_WithNewUrnFormat_Success()
    {
        // Arrange
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.Sender = "urn:altinn:organization:identifier-no:991825827";
        initializeRequestBody.Recipients = new List<string> { "urn:altinn:organization:identifier-no:986252932" };

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);
        
        // Assert
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        Assert.NotNull(fileTransferResponse);
    }

    [Fact]
    public async Task InitializeFileTransfer_WithMixedUrnFormats_Success()
    {
        // Arrange
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.Sender = "urn:altinn:organization:identifier-no:991825827";
        initializeRequestBody.Recipients = new List<string> 
        { 
            "urn:altinn:organization:identifier-no:986252932",
            "0192:910351192" 
        };

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);
        
        // Assert
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        Assert.NotNull(fileTransferResponse);
    }

    [Fact]
    public async Task Initialize_WithUrnResourceId_Success()
    {
        // Arrange
        var initializeRequestBody = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        initializeRequestBody.ResourceId = $"urn:altinn:resource:{TestConstants.RESOURCE_FOR_TEST}";

        // Act
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequestBody);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();

        // Assert
        Assert.NotNull(fileTransferResponse);
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();
        var fileTransferAfterInitialize = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _responseSerializerOptions);
        Assert.NotNull(fileTransferAfterInitialize);
        Assert.True(fileTransferAfterInitialize.FileTransferStatus == FileTransferStatusExt.Initialized);
    }


    [Fact]
    public async Task DownloadFileTransfer_ResourceWithPurgeGraceTime_SchedulesDeletionInGracePeriod()
    {
        // Arrange
        var resource = new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P48H",
            PurgeFileTransferAfterAllRecipientsConfirmed = true,
            PurgeFileTransferGracePeriod = "PT24H"
        };
        var createResponse = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_WITH_GRACEFUL_PURGE}", resource);
        var fileTransfer = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        fileTransfer.ResourceId = TestConstants.RESOURCE_WITH_GRACEFUL_PURGE;
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", fileTransfer);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<API.Models.FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse?.FileTransferId.ToString();
        Assert.NotNull(fileTransferId);
        await UploadDummyFileTransferAsync(fileTransferId);
        var downloadResponse = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.True(downloadResponse.IsSuccessStatusCode, await downloadResponse.Content.ReadAsStringAsync());
        
        // Act
        var confirmResponse = await _recipientClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/confirmdownload", null);
        Assert.True(confirmResponse.IsSuccessStatusCode, await confirmResponse.Content.ReadAsStringAsync());

        // Assert that the deletion is scheduled in the grace period
        var jobStorage = _factory.Services.GetService(typeof(JobStorage)) as JobStorage;
        var gracePeriod = XmlConvert.ToTimeSpan("PT24H");
        
        Assert.NotNull(jobStorage.GetMonitoringApi().ScheduledJobs(0, 100).SingleOrDefault(j => 
            j.Value.Job.Method.Name == "Process" && 
            ((PurgeFileTransferRequest)j.Value.Job.Args[0]).FileTransferId.ToString() == fileTransferId && 
            ((PurgeFileTransferRequest)j.Value.Job.Args[0]).PurgeTrigger == PurgeTrigger.AllConfirmedDownloaded &&
            j.Value.EnqueueAt > DateTime.UtcNow.Add(gracePeriod).AddMinutes(-1) && 
            j.Value.EnqueueAt < DateTime.UtcNow.Add(gracePeriod).AddMinutes(1)).Value);
    }

    [Fact]
    public async Task ConfirmDownload_ResourceWithPurgeGraceTime_ExpiresAfterAllConfirmedDownloadedButBeforePurge()
    {
        // Arrange
        var resource = new ResourceExt
        {
            MaxFileTransferSize = 1000000,
            FileTransferTimeToLive = "P48H",
            PurgeFileTransferAfterAllRecipientsConfirmed = true,
            PurgeFileTransferGracePeriod = "PT24H"
        };
        var createResponse = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_WITH_GRACEFUL_PURGE}", resource);
        var fileTransfer = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        fileTransfer.ResourceId = TestConstants.RESOURCE_WITH_GRACEFUL_PURGE;
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", fileTransfer);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<API.Models.FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse?.FileTransferId.ToString();
        Assert.NotNull(fileTransferId);
        await UploadDummyFileTransferAsync(fileTransferId);
        var downloadResponse = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.True(downloadResponse.IsSuccessStatusCode, await downloadResponse.Content.ReadAsStringAsync());
        var confirmResponse = await _recipientClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/confirmdownload", null);
        Assert.True(confirmResponse.IsSuccessStatusCode, await confirmResponse.Content.ReadAsStringAsync());

        // Act
        using var requestScope = _factory.Services.CreateScope();
        var purgeFileTransferHandler = requestScope.ServiceProvider.GetRequiredService<PurgeFileTransferHandler>();
        var fileExpiryPurgeResult = await purgeFileTransferHandler.Process(new PurgeFileTransferRequest
        {
            FileTransferId = Guid.Parse(fileTransferId),
            PurgeTrigger = PurgeTrigger.FileTransferExpiry
        }, null, CancellationToken.None);
        Assert.Equal(Task.CompletedTask, fileExpiryPurgeResult);
        var fileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(fileTransferDetails);
        Assert.Equal(FileTransferStatusExt.AllConfirmedDownloaded, fileTransferDetails.FileTransferStatus);
        var allConfirmedDownloadedPurgeResult = await purgeFileTransferHandler.Process(new PurgeFileTransferRequest
        {
            FileTransferId = Guid.Parse(fileTransferId),
            PurgeTrigger = PurgeTrigger.AllConfirmedDownloaded
        }, null, CancellationToken.None);
        fileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);
        Assert.NotNull(fileTransferDetails);

        // Assert
        Assert.Equal(FileTransferStatusExt.Purged, fileTransferDetails.FileTransferStatus);

    }

    private async Task<HttpResponseMessage> UploadTextFileTransfer(string fileTransferId, string fileContent)
    {
        var fileContents = Encoding.UTF8.GetBytes(fileContent);
        using (var content = new ByteArrayContent(fileContents))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            return uploadResponse;
        }
    }

    private async Task UploadDummyFileTransferAsync(string fileTransferId)
    {
        var fileContents = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");
        using (var content = new ByteArrayContent(fileContents))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await _senderClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/upload", content);
            Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
        }
    }
    private async Task<string> InitializeAndAssertBasicFileTransfer()
    {
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());

        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<API.Models.FileTransferInitializeResponseExt>();
        return fileTransferResponse?.FileTransferId.ToString() ?? string.Empty;
    }

    private string CalculateChecksum(byte[] data)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
    private async Task Test_Graceful_purge_changes_purge_time(string time = "PT12H")
    {
        var response = await _serviceOwnerClient.PutAsJsonAsync($"broker/api/v1/resource/{TestConstants.RESOURCE_FOR_TEST}", new ResourceExt
        {
            PurgeFileTransferAfterAllRecipientsConfirmed = true,
            PurgeFileTransferGracePeriod = time
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var fileContent = "This is the contents of the uploaded file";
        var fileContentBytes = Encoding.UTF8.GetBytes(fileContent);
        var file = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        var initializeFileTransferResponse = await _senderClient.PostAsJsonAsync("broker/api/v1/filetransfer", file);
        Assert.True(initializeFileTransferResponse.IsSuccessStatusCode, await initializeFileTransferResponse.Content.ReadAsStringAsync());
        var fileTransferResponse = await initializeFileTransferResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        var fileTransferId = fileTransferResponse.FileTransferId.ToString();

        var jobstorage = _factory.Services.GetService(typeof(JobStorage)) as JobStorage;
        var uploadResponse = await UploadTextFileTransfer(fileTransferId, fileContent);

        Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());

        var downloadResponse = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.True(downloadResponse.IsSuccessStatusCode, await downloadResponse.Content.ReadAsStringAsync());

        Assert.NotNull(jobstorage.GetMonitoringApi().ScheduledJobs(0, 100).SingleOrDefault(j => j.Value.Job.Method.Name == "Process" && ((PurgeFileTransferRequest)j.Value.Job.Args[0]).FileTransferId.ToString() == fileTransferId && ((PurgeFileTransferRequest)j.Value.Job.Args[0]).PurgeTrigger == PurgeTrigger.FileTransferExpiry).Value);
        var confirmResponse = await _recipientClient.PostAsync($"broker/api/v1/filetransfer/{fileTransferId}/confirmdownload", null);
        var confirmedFileTransferDetails = await _senderClient.GetFromJsonAsync<FileTransferStatusDetailsExt>($"broker/api/v1/filetransfer/{fileTransferId}/details", _responseSerializerOptions);

        var gracePeriod = XmlConvert.ToTimeSpan(time);

        Assert.NotNull(confirmedFileTransferDetails);
        Assert.True(confirmedFileTransferDetails.FileTransferStatus == FileTransferStatusExt.AllConfirmedDownloaded);
        Assert.Null(jobstorage.GetMonitoringApi().ScheduledJobs(0, 100).SingleOrDefault(j => j.Value.Job.Method.Name == "Process" && ((PurgeFileTransferRequest)j.Value.Job.Args[0]).FileTransferId.ToString() == fileTransferId && ((PurgeFileTransferRequest)j.Value.Job.Args[0]).PurgeTrigger == PurgeTrigger.FileTransferExpiry).Value);
        Assert.NotNull(jobstorage.GetMonitoringApi().ScheduledJobs(0, 100).SingleOrDefault(j => j.Value.Job.Method.Name == "Process" && ((PurgeFileTransferRequest)j.Value.Job.Args[0]).FileTransferId.ToString() == fileTransferId && ((PurgeFileTransferRequest)j.Value.Job.Args[0]).PurgeTrigger == PurgeTrigger.AllConfirmedDownloaded &&
        j.Value.EnqueueAt > DateTime.UtcNow.Add(gracePeriod).AddMinutes(-1) && j.Value.EnqueueAt < DateTime.UtcNow.Add(gracePeriod).AddMinutes(1)).Value);

    }
}
