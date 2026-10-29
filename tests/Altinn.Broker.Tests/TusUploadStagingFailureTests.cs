using System.Net;
using System.Text;

using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;

public class TusUploadStagingFailureTests : IClassFixture<TusStagingFailureTestFactory>
{
    private readonly TusStagingFailureTestFactory _factory;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _recipientClient;

    public TusUploadStagingFailureTests(TusStagingFailureTestFactory factory)
    {
        _factory = factory;
        _senderClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _recipientClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_RECIPIENT_TOKEN);
        _factory.StagingFailures.Clear();
    }

    [Fact]
    public async Task TusUpload_StagingFailure_ReconcilesHeadOffsetAndResumes()
    {
        var fileContent = Encoding.UTF8.GetBytes("abcdefghijklmnopqrstuvwxyz0123"); // 30 bytes
        const int chunkSize = 10;

        var (fileTransferId, uploadUrl) = await TusUploadTestHelper.InitializeAndCreateTusUploadAsync(
            _senderClient,
            fileContent.Length);

        // Chunk 1 stages successfully and HEAD reports durable offset.
        var chunk1 = fileContent.AsSpan(0, chunkSize).ToArray();
        var patchOffset1 = await TusUploadTestHelper.PatchChunkAsync(_senderClient, uploadUrl, offset: 0, chunk1);
        Assert.Equal(chunkSize, patchOffset1);

        var durableAfterChunk1 = await TusUploadTestHelper.WaitForHeadOffsetAsync(
            _senderClient,
            uploadUrl,
            expectedOffset: chunkSize,
            timeout: TimeSpan.FromSeconds(15));
        Assert.Equal(chunkSize, durableAfterChunk1);

        // Chunk 2 is accepted by TUS, then Azure staging fails asynchronously.
        // Reconcile must roll AcceptedOffset back so HEAD/PATCH agree on the durable offset.
        _factory.StagingFailures.FailNextStages(1);
        var chunk2 = fileContent.AsSpan(chunkSize, chunkSize).ToArray();
        var patchOffset2 = await TusUploadTestHelper.PatchChunkAsync(
            _senderClient,
            uploadUrl,
            offset: durableAfterChunk1,
            chunk2);
        Assert.Equal(chunkSize * 2, patchOffset2);

        var headAfterFailure = await TusUploadTestHelper.WaitForHeadOffsetAsync(
            _senderClient,
            uploadUrl,
            expectedOffset: durableAfterChunk1,
            timeout: TimeSpan.FromSeconds(15));
        Assert.Equal(durableAfterChunk1, headAfterFailure);

        // Resume from HEAD: PATCH validation must accept the durable offset (not the abandoned Accepted).
        var remaining = fileContent.AsSpan((int)headAfterFailure).ToArray();
        var resumePatchOffset = await TusUploadTestHelper.PatchChunkAsync(
            _senderClient,
            uploadUrl,
            offset: headAfterFailure,
            remaining);
        Assert.Equal(fileContent.Length, resumePatchOffset);

        await TusUploadTestHelper.WaitForPublishedAndAssertDownloadAsync(
            _senderClient,
            _recipientClient,
            fileTransferId,
            fileContent);
    }

    [Fact]
    public async Task TusUpload_StagingFailure_PatchAtStaleAcceptedOffset_ConflictsThenHeadResumes()
    {
        var fileContent = Encoding.UTF8.GetBytes("abcdefghijklmnopqrstuvwxyz0123"); // 30 bytes
        const int chunkSize = 10;

        var (fileTransferId, uploadUrl) = await TusUploadTestHelper.InitializeAndCreateTusUploadAsync(
            _senderClient,
            fileContent.Length);

        var chunk1 = fileContent.AsSpan(0, chunkSize).ToArray();
        await TusUploadTestHelper.PatchChunkAsync(_senderClient, uploadUrl, offset: 0, chunk1);
        await TusUploadTestHelper.WaitForHeadOffsetAsync(
            _senderClient,
            uploadUrl,
            expectedOffset: chunkSize,
            timeout: TimeSpan.FromSeconds(15));

        _factory.StagingFailures.FailNextStages(1);
        var chunk2 = fileContent.AsSpan(chunkSize, chunkSize).ToArray();
        var abandonedAcceptedOffset = await TusUploadTestHelper.PatchChunkAsync(
            _senderClient,
            uploadUrl,
            offset: chunkSize,
            chunk2);
        Assert.Equal(chunkSize * 2, abandonedAcceptedOffset);

        // Wait until reconcile has rolled HEAD back to durable storage.
        var durableOffset = await TusUploadTestHelper.WaitForHeadOffsetAsync(
            _senderClient,
            uploadUrl,
            expectedOffset: chunkSize,
            timeout: TimeSpan.FromSeconds(15));
        Assert.Equal(chunkSize, durableOffset);

        // A client that reused the PATCH response offset (pre-reconcile) must get 409,
        // then recover via HEAD.
        var stalePatch = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);
        stalePatch.Headers.Add("Tus-Resumable", "1.0.0");
        stalePatch.Headers.Add("Upload-Offset", abandonedAcceptedOffset.ToString());
        stalePatch.Content = new ByteArrayContent(fileContent.AsSpan(chunkSize * 2).ToArray());
        stalePatch.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/offset+octet-stream");
        using (var staleResponse = await _senderClient.SendAsync(stalePatch))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        }

        var headOffset = await TusUploadTestHelper.HeadUploadOffsetAsync(_senderClient, uploadUrl);
        Assert.Equal(durableOffset, headOffset);

        var remaining = fileContent.AsSpan((int)headOffset).ToArray();
        var resumedOffset = await TusUploadTestHelper.PatchChunkAsync(
            _senderClient,
            uploadUrl,
            offset: headOffset,
            remaining);
        Assert.Equal(fileContent.Length, resumedOffset);

        await TusUploadTestHelper.WaitForPublishedAndAssertDownloadAsync(
            _senderClient,
            _recipientClient,
            fileTransferId,
            fileContent);
    }
}
