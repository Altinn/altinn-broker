using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Altinn.Broker.API.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Helpers;

using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace Altinn.Broker.Tests;

public class InitializeAndUploadFormDataTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public InitializeAndUploadFormDataTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _senderClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = _factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeAndUpload_WhenAllIsOK_Success()
    {
        // Arrange
        var fileContent = "This is the contents of the uploaded file";
        var metadata = new FileTransferInitalizeExt
        {
            FileName = "test.txt",
            ResourceId = TestConstants.RESOURCE_FOR_TEST,
            Sender = "0192:991825827",
            SendersFileTransferReference = "CaseFiles-123",
            Recipients = new List<string> { "0192:986252932" },
            PropertyList = new Dictionary<string, string> { { "key1", "value1" } }
        };

        // Act - Initialize and upload in one request
        var multipart = CreateMultipartFormData(metadata, fileContent);
        var response = await _senderClient.PostAsync("broker/api/v1/filetransfer/upload", multipart);

        // Assert - Upload succeeded
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var uploadResponse = await response.Content.ReadFromJsonAsync<FileTransferUploadResponseExt>(_jsonOptions);
        Assert.NotNull(uploadResponse);
        var fileTransferId = uploadResponse.FileTransferId.ToString();

        // Verify file transfer status is Published
        var fileTransferOverview = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>($"broker/api/v1/filetransfer/{fileTransferId}", _jsonOptions);
        Assert.NotNull(fileTransferOverview);
        Assert.Equal(FileTransferStatusExt.Published, fileTransferOverview.FileTransferStatus);

        // Verify file can be downloaded by recipient
        var downloadedFile = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.True(downloadedFile.IsSuccessStatusCode);
        var downloadedFileBytes = await downloadedFile.Content.ReadAsByteArrayAsync();
        var expectedFileBytes = Encoding.UTF8.GetBytes(fileContent);
        Assert.Equal(expectedFileBytes, downloadedFileBytes);
    }

    [Fact]
    public async Task InitializeAndUpload_UnauthorizedSender_Returns401()
    {
        var metadata = new FileTransferInitalizeExt
        {
            FileName = "test.txt",
            ResourceId = TestConstants.RESOURCE_WITH_NO_ACCESS,
            Sender = "0192:991825827",
            Recipients = new List<string> { "0192:986252932" },
            SendersFileTransferReference = "123",
            PropertyList = new Dictionary<string, string> { { "k1", "v1" } }
        };

        var multipart = CreateMultipartFormData(metadata, "hello world");

        var resp = await _senderClient.PostAsync("broker/api/v1/filetransfer/upload", multipart);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry", problem.Detail);
    }

    [Fact]
    public async Task InitializeAndUpload_MissingFile_Returns400()
    {
        var metadata = new FileTransferInitalizeExt
        {
            FileName = "test.txt",
            ResourceId = TestConstants.RESOURCE_FOR_TEST,
            Sender = "0192:991825827",
            SendersFileTransferReference = "123",
            Recipients = new List<string> { "0192:986252932" },
            PropertyList = new Dictionary<string, string>()
        };

        var multipart = new MultipartFormDataContent();
        
        // Add metadata fields but intentionally omit the FileTransfer part
        multipart.Add(new StringContent(metadata.FileName), "Metadata.FileName");
        multipart.Add(new StringContent(metadata.ResourceId), "Metadata.ResourceId");
        multipart.Add(new StringContent(metadata.Sender), "Metadata.Sender");
        foreach (var recipient in metadata.Recipients)
        {
            multipart.Add(new StringContent(recipient), "Metadata.Recipients");
        }

        var resp = await _senderClient.PostAsync("broker/api/v1/filetransfer/upload", multipart);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        Assert.NotNull(problem);
    }

    private MultipartFormDataContent CreateMultipartFormData(FileTransferInitalizeExt metadata, string fileContent)
    {
        var multipart = new MultipartFormDataContent();
        
        // Add metadata as individual form fields with Metadata. prefix
        multipart.Add(new StringContent(metadata.FileName), "Metadata.FileName");
        multipart.Add(new StringContent(metadata.ResourceId), "Metadata.ResourceId");
        if (!string.IsNullOrEmpty(metadata.SendersFileTransferReference))
        {
            multipart.Add(new StringContent(metadata.SendersFileTransferReference), "Metadata.SendersFileTransferReference");
        }
        multipart.Add(new StringContent(metadata.Sender), "Metadata.Sender");
        
        // Add recipients - ASP.NET Core model binding should handle multiple values with the same key
        foreach (var recipient in metadata.Recipients)
        {
            multipart.Add(new StringContent(recipient), "Metadata.Recipients");
        }
        
        if (metadata.DisableVirusScan.HasValue)
        {
            multipart.Add(new StringContent(metadata.DisableVirusScan.Value.ToString().ToLower()), "Metadata.DisableVirusScan");
        }
        
        // Add property list if present
        if (metadata.PropertyList != null && metadata.PropertyList.Count > 0)
        {
            foreach (var property in metadata.PropertyList)
            {
                multipart.Add(new StringContent(property.Value), $"Metadata.PropertyList[{property.Key}]");
            }
        }
        
        if (!string.IsNullOrEmpty(metadata.Checksum))
        {
            multipart.Add(new StringContent(metadata.Checksum), "Metadata.Checksum");
        }

        // Add file
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var fileContentBytes = new ByteArrayContent(fileBytes);
        fileContentBytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(fileContentBytes, nameof(FileTransferInitializeAndUploadExt.FileTransfer), "FileTransfer");

        return multipart;
    }
}


