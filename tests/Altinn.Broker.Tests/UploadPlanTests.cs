using System.Net;
using System.Net.Http.Json;
using System.Text;

using Altinn.Broker.API.Models;
using Altinn.Broker.Application.UploadPlans;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;

public class UploadPlanTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;

    public UploadPlanTests(CustomWebApplicationFactory factory)
    {
        _senderClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN, allowAutoRedirect: false);
        _recipientClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
    }

    [Fact]
    public async Task UploadPlan_SplitUpload_CanBeReadBackWithProgress()
    {
        var fileTransferId = await InitializeAsync();
        var partA = await CreatePartialAsync(fileTransferId, 20);
        var partB = await CreatePartialAsync(fileTransferId, 10);

        var putResponse = await PutPlanAsync(fileTransferId, (UploadId(partA), 0, 20), (UploadId(partB), 20, 10));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        await TusUploadTestHelper.PatchChunkAsync(_senderClient, partA, 0, Encoding.UTF8.GetBytes("abcdefghij"));
        await TusUploadTestHelper.WaitForHeadOffsetAsync(_senderClient, partA, expectedOffset: 10, timeout: TimeSpan.FromSeconds(30));

        var plan = await GetPlanAsync(fileTransferId);
        Assert.Equal("fingerprint", plan.Fingerprint);
        Assert.Equal([UploadId(partA), UploadId(partB)], plan.Parts.Select(part => part.UploadId));
        Assert.Equal([0L, 20L], plan.Parts.Select(part => part.Start));
        Assert.Equal(10, plan.Parts[0].UploadedBytes);
        Assert.Equal(0, plan.Parts[1].UploadedBytes);
        Assert.Equal(10, plan.UploadedBytes);
    }

    [Fact]
    public async Task UploadPlan_SingleUpload_IsSavedUnderTheTransferId()
    {
        var (fileTransferId, uploadUrl) = await TusUploadTestHelper.InitializeAndCreateTusUploadAsync(_senderClient, 30);

        var putResponse = await PutPlanAsync(fileTransferId, (UploadId(uploadUrl), 0, 30));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var plan = await GetPlanAsync(fileTransferId);
        Assert.Equal(fileTransferId, plan.Parts.Single().UploadId);
    }

    [Fact]
    public async Task UploadPlan_PartialLeftBehindByARetriedCreate_IsLeftOutOfThePlan()
    {
        var fileTransferId = await InitializeAsync();
        var partA = await CreatePartialAsync(fileTransferId, 20);
        await CreatePartialAsync(fileTransferId, 10); // as if its response was lost and the create retried
        var partB = await CreatePartialAsync(fileTransferId, 10);

        var putResponse = await PutPlanAsync(fileTransferId, (UploadId(partA), 0, 20), (UploadId(partB), 20, 10));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var plan = await GetPlanAsync(fileTransferId);
        Assert.Equal([UploadId(partA), UploadId(partB)], plan.Parts.Select(part => part.UploadId));
    }

    [Fact]
    public async Task UploadPlan_PartLengthDoesNotMatchUpload_IsRejected()
    {
        var fileTransferId = await InitializeAsync();
        var partA = await CreatePartialAsync(fileTransferId, 20);
        var partB = await CreatePartialAsync(fileTransferId, 10);

        var putResponse = await PutPlanAsync(fileTransferId, (UploadId(partA), 0, 20), (UploadId(partB), 20, 11));

        Assert.Equal(HttpStatusCode.BadRequest, putResponse.StatusCode);
        var getResponse = await _senderClient.GetAsync(PlanUrl(fileTransferId));
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task UploadPlan_PartOfAnotherTransfer_IsRejected()
    {
        var fileTransferId = await InitializeAsync();
        var otherFileTransferId = await InitializeAsync();
        var partA = await CreatePartialAsync(fileTransferId, 20);
        var otherPart = await CreatePartialAsync(otherFileTransferId, 10);

        var putResponse = await PutPlanAsync(fileTransferId, (UploadId(partA), 0, 20), (UploadId(otherPart), 20, 10));

        Assert.Equal(HttpStatusCode.BadRequest, putResponse.StatusCode);
    }

    [Fact]
    public async Task UploadPlan_DiscardedUpload_IsNoLongerOffered()
    {
        var fileTransferId = await InitializeAsync();
        var partA = await CreatePartialAsync(fileTransferId, 20);
        var partB = await CreatePartialAsync(fileTransferId, 10);
        var putResponse = await PutPlanAsync(fileTransferId, (UploadId(partA), 0, 20), (UploadId(partB), 20, 10));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, partA);
        deleteRequest.Headers.Add("Tus-Resumable", "1.0.0");
        var deleteResponse = await _senderClient.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _senderClient.GetAsync(PlanUrl(fileTransferId));
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task UploadPlan_Recipient_HasNoAccess()
    {
        var (fileTransferId, _) = await TusUploadTestHelper.InitializeAndCreateTusUploadAsync(_senderClient, 30);
        var putResponse = await PutPlanAsync(fileTransferId, (fileTransferId, 0, 30));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await _recipientClient.GetAsync(PlanUrl(fileTransferId));
        Assert.True(
            getResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)getResponse.StatusCode}");
    }

    private async Task<string> InitializeAsync()
    {
        var initializeResponse = await _senderClient.PostAsJsonAsync(
            "broker/api/v1/filetransfer",
            FileTransferInitializeExtTestFactory.BasicFileTransfer());
        Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
        var initializeResult = await initializeResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>(
            TusUploadTestHelper.SerializerOptions);
        Assert.NotNull(initializeResult);
        return initializeResult.FileTransferId.ToString();
    }

    private async Task<Uri> CreatePartialAsync(string fileTransferId, long length)
    {
        var createRequest = new HttpRequestMessage(HttpMethod.Post, $"broker/api/v1/filetransfer/upload/tus/{fileTransferId}");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", length.ToString());
        createRequest.Headers.Add("Upload-Concat", "partial");
        var createResponse = await _senderClient.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        return createResponse.Headers.Location;
    }

    private Task<HttpResponseMessage> PutPlanAsync(string fileTransferId, params (string UploadId, long Start, long Length)[] parts)
        => _senderClient.PutAsJsonAsync(PlanUrl(fileTransferId), new SaveUploadPlanRequest
        {
            Fingerprint = "fingerprint",
            FingerprintVersion = 1,
            Parts = parts.Select(part => new UploadPlanPartRequest { UploadId = part.UploadId, Start = part.Start, Length = part.Length }).ToList(),
        });

    private async Task<GetUploadPlanResponse> GetPlanAsync(string fileTransferId)
    {
        var response = await _senderClient.GetAsync(PlanUrl(fileTransferId));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var plan = await response.Content.ReadFromJsonAsync<GetUploadPlanResponse>(TusUploadTestHelper.SerializerOptions);
        Assert.NotNull(plan);
        return plan;
    }

    private static string PlanUrl(string fileTransferId) => $"broker/api/v1/frontend/file-transfers/{fileTransferId}/upload-plan";

    private static string PathOf(Uri uploadUrl) => uploadUrl.IsAbsoluteUri ? uploadUrl.AbsolutePath : uploadUrl.ToString();

    private static string UploadId(Uri uploadUrl) => PathOf(uploadUrl).TrimEnd('/').Split('/').Last();
}
