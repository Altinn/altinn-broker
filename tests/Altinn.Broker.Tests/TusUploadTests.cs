using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Altinn.Broker.API.Models;
using Altinn.Broker.Common.Constants;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;

public class TusUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;

    public TusUploadTests(CustomWebApplicationFactory factory)
    {
        _senderClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
    }

    [Fact]
    public async Task TusUpload_SmallFile_SucceedsAndCanBeDownloaded()
    {
        var initializeResponse = await _senderClient.PostAsJsonAsync(
            "broker/api/v1/filetransfer",
            FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());

        var initializeResult = await initializeResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>();
        Assert.NotNull(initializeResult);
        var fileTransferId = initializeResult.FileTransferId.ToString();
        var fileContent = Encoding.UTF8.GetBytes("This is the contents of the uploaded file");

        var tusBaseUrl = $"broker/api/v1/filetransfer/upload/tus/{fileTransferId}";

        var optionsRequest = new HttpRequestMessage(HttpMethod.Options, tusBaseUrl);
        optionsRequest.Headers.Add("Tus-Resumable", "1.0.0");
        var optionsResponse = await _senderClient.SendAsync(optionsRequest);
        Assert.Equal(HttpStatusCode.NoContent, optionsResponse.StatusCode);

        var createRequest = new HttpRequestMessage(HttpMethod.Post, tusBaseUrl);
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", fileContent.Length.ToString());
        var createResponse = await _senderClient.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var uploadUrl = createResponse.Headers.Location;
        Assert.NotNull(uploadUrl);

        var headRequest = new HttpRequestMessage(HttpMethod.Head, uploadUrl);
        headRequest.Headers.Add("Tus-Resumable", "1.0.0");
        var headResponse = await _senderClient.SendAsync(headRequest);
        Assert.Equal(HttpStatusCode.OK, headResponse.StatusCode);
        Assert.True(headResponse.Headers.TryGetValues("Upload-Offset", out var offsetValues));
        Assert.Equal("0", offsetValues.First());

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);
        patchRequest.Headers.Add("Tus-Resumable", "1.0.0");
        patchRequest.Headers.Add("Upload-Offset", "0");
        patchRequest.Content = new ByteArrayContent(fileContent);
        patchRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        var patchResponse = await _senderClient.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var overview = await _senderClient.GetFromJsonAsync<FileTransferOverviewExt>(
            $"broker/api/v1/filetransfer/{fileTransferId}");
        Assert.NotNull(overview);
        Assert.Equal(FileTransferStatusExt.Published, overview.FileTransferStatus);

        var downloadResponse = await _recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.True(downloadResponse.IsSuccessStatusCode, await downloadResponse.Content.ReadAsStringAsync());
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileContent, downloadedBytes);
    }
}
