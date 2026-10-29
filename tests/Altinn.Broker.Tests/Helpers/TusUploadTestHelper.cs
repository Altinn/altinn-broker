using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Broker.API.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;

internal static class TusUploadTestHelper
{
    public static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static async Task<(string FileTransferId, Uri UploadUrl)> InitializeAndCreateTusUploadAsync(
        HttpClient senderClient,
        long uploadLength)
    {
        var initializeResponse = await senderClient.PostAsJsonAsync(
            "broker/api/v1/filetransfer",
            FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());

        var initializeResult = await initializeResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>(
            SerializerOptions);
        Assert.NotNull(initializeResult);
        var fileTransferId = initializeResult.FileTransferId.ToString();

        var tusBaseUrl = $"broker/api/v1/filetransfer/upload/tus/{fileTransferId}";
        var createRequest = new HttpRequestMessage(HttpMethod.Post, tusBaseUrl);
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", uploadLength.ToString());
        var createResponse = await senderClient.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var uploadUrl = createResponse.Headers.Location;
        Assert.NotNull(uploadUrl);
        return (fileTransferId, uploadUrl);
    }

    public static async Task<long> PatchChunkAsync(
        HttpClient senderClient,
        Uri uploadUrl,
        long offset,
        byte[] payload)
    {
        var patchResponse = await senderClient.SendAsync(PatchRequest(uploadUrl, offset, new ByteArrayContent(payload)));
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);
        Assert.True(patchResponse.Headers.TryGetValues("Upload-Offset", out var offsetValues));
        Assert.True(long.TryParse(offsetValues.First(), out var responseOffset));
        return responseOffset;
    }

    public static HttpRequestMessage PatchRequest(Uri uploadUrl, long offset, HttpContent body)
    {
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);
        patchRequest.Headers.Add("Tus-Resumable", "1.0.0");
        patchRequest.Headers.Add("Upload-Offset", offset.ToString());
        patchRequest.Content = body;
        patchRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        return patchRequest;
    }

    public static async Task<(HttpStatusCode StatusCode, long? Offset, string Body)> TryHeadUploadAsync(
        HttpClient senderClient,
        Uri uploadUrl)
    {
        var headRequest = new HttpRequestMessage(HttpMethod.Head, uploadUrl);
        headRequest.Headers.Add("Tus-Resumable", "1.0.0");
        var headResponse = await senderClient.SendAsync(headRequest);
        var body = headResponse.Content is null
            ? string.Empty
            : await headResponse.Content.ReadAsStringAsync();

        long? offset = null;
        if (headResponse.Headers.TryGetValues("Upload-Offset", out var offsetValues)
            && long.TryParse(offsetValues.FirstOrDefault(), out var parsed))
        {
            offset = parsed;
        }

        return (headResponse.StatusCode, offset, body);
    }

    public static async Task<long> HeadUploadOffsetAsync(HttpClient senderClient, Uri uploadUrl)
    {
        var (statusCode, offset, body) = await TryHeadUploadAsync(senderClient, uploadUrl);
        Assert.True(
            statusCode == HttpStatusCode.OK,
            $"TUS HEAD expected 200 OK but got {(int)statusCode} {statusCode}. Body: {body}");
        Assert.True(offset.HasValue, "TUS HEAD did not return Upload-Offset.");
        return offset.Value;
    }

    public static async Task<long> WaitForHeadOffsetAsync(
        HttpClient senderClient,
        Uri uploadUrl,
        long expectedOffset,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        long? lastOffset = null;
        HttpStatusCode lastStatus = 0;
        var lastBody = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            var (statusCode, offset, body) = await TryHeadUploadAsync(senderClient, uploadUrl);
            lastStatus = statusCode;
            lastBody = body;
            lastOffset = offset;

            // Staging/reconcile can briefly fault the in-memory upload state; retry HEAD.
            if (statusCode == HttpStatusCode.OK && offset == expectedOffset)
            {
                return offset.Value;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for TUS HEAD Upload-Offset={expectedOffset}. " +
            $"Last status={(int)lastStatus} {lastStatus}, offset={lastOffset?.ToString() ?? "<none>"}, body={lastBody}");
    }

    public static async Task WaitForPublishedAndAssertDownloadAsync(
        HttpClient senderClient,
        HttpClient recipientClient,
        string fileTransferId,
        byte[] expectedContent)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        FileTransferOverviewExt? overview = null;
        while (DateTime.UtcNow < deadline)
        {
            overview = await senderClient.GetFromJsonAsync<FileTransferOverviewExt>(
                $"broker/api/v1/filetransfer/{fileTransferId}",
                SerializerOptions);
            if (overview?.FileTransferStatus == FileTransferStatusExt.Published)
            {
                break;
            }

            await Task.Delay(250);
        }

        Assert.NotNull(overview);
        Assert.Equal(FileTransferStatusExt.Published, overview.FileTransferStatus);

        var downloadResponse = await recipientClient.GetAsync($"broker/api/v1/filetransfer/{fileTransferId}/download");
        Assert.True(downloadResponse.IsSuccessStatusCode, await downloadResponse.Content.ReadAsStringAsync());
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(expectedContent, downloadedBytes);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
