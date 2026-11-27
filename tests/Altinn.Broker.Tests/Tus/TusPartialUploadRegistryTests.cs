using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusPartialUploadRegistryTests
{
    private const string FileTransferId = "c28803a5-c986-4bb6-ac28-b47aafffad16";
    private const string PartialUploadId = "43b05d2efb1c444eaff5c53e1cbee918";

    [Fact]
    public async Task TryGetFileTransferIdAsync_UnknownPartialId_DoesNotReturnPartialIdAsFileTransferId()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();

        var fileTransferId = await registry.TryGetFileTransferIdAsync(PartialUploadId, CancellationToken.None);

        Assert.Null(fileTransferId);
    }

    [Fact]
    public async Task RegisterPartialAsync_ConcurrentCreates_KeepIndexAndBaseOffsetInStep()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        var fileTransferId = Guid.NewGuid();
        var partialIds = Enumerable.Range(0, 32).Select(_ => Guid.NewGuid().ToString("N")).ToList();

        // PartialIndex and BaseOffset come from two counters. Bumping them in separate round trips lets an
        // interleaved create pair a lower PartialIndex with a higher BaseOffset, misordering assembly.
        await Task.WhenAll(partialIds.Select((partialId, i) =>
            registry.RegisterPartialAsync(partialId, fileTransferId, uploadLength: 100 + i, CancellationToken.None)));

        var partials = new List<PartialUploadInfo>();
        foreach (var partialId in partialIds)
        {
            var info = await registry.TryGetPartialInfoAsync(partialId, CancellationToken.None);
            Assert.NotNull(info);
            partials.Add(info!.Value);
        }

        Assert.Equal(partialIds.Count, partials.Select(partial => partial.PartialIndex).Distinct().Count());

        var expectedOffset = 0L;
        foreach (var partial in partials.OrderBy(partial => partial.PartialIndex))
        {
            Assert.Equal(expectedOffset, partial.BaseOffset);
            expectedOffset += partial.UploadLength;
        }

        Assert.Equal(expectedOffset, await registry.PeekNextBaseOffsetAsync(fileTransferId, CancellationToken.None));
    }

    [Fact]
    public async Task IncrementStripeBlockCountAsync_CountsPerStripeAndClears()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        var fileTransferId = Guid.NewGuid();

        Assert.Equal(1, await registry.IncrementStripeBlockCountAsync(fileTransferId, 0, 1, CancellationToken.None));
        Assert.Equal(2, await registry.IncrementStripeBlockCountAsync(fileTransferId, 0, 1, CancellationToken.None));
        // A different stripe of the same transfer has its own budget.
        Assert.Equal(1, await registry.IncrementStripeBlockCountAsync(fileTransferId, 1, 1, CancellationToken.None));

        await registry.ClearStripeBlockCountsAsync(fileTransferId, CancellationToken.None);

        Assert.Equal(1, await registry.IncrementStripeBlockCountAsync(fileTransferId, 0, 1, CancellationToken.None));
        Assert.Equal(1, await registry.IncrementStripeBlockCountAsync(fileTransferId, 1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task TryGetFileTransferIdAsync_RegisteredPartial_ReturnsParentFileTransferId()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        await registry.RegisterPartialAsync(
            PartialUploadId,
            Guid.Parse(FileTransferId),
            uploadLength: 1024,
            CancellationToken.None);

        var fileTransferId = await registry.TryGetFileTransferIdAsync(PartialUploadId, CancellationToken.None);

        Assert.Equal(Guid.Parse(FileTransferId), fileTransferId);
    }

    [Fact]
    public async Task TryGetUploadLengthAsync_LengthEntryEvicted_RecoversLengthFromPartialInfo()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        await registry.RegisterPartialAsync(
            PartialUploadId,
            Guid.Parse(FileTransferId),
            uploadLength: 4096,
            CancellationToken.None);

        // A partial's length is written under two keys, which can be evicted independently. Reading zero
        // here would let a concatenation pass a size limit the assembled transfer then exceeds.
        await scope.DistributedCache.RemoveAsync($"tus-upload-length:{PartialUploadId}", CancellationToken.None);

        Assert.Equal(4096, await registry.TryGetUploadLengthAsync(PartialUploadId, CancellationToken.None));
    }

    [Fact]
    public async Task TryGetUploadLengthAsync_RemovedPartial_ReturnsNull()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        await registry.RegisterPartialAsync(
            PartialUploadId,
            Guid.Parse(FileTransferId),
            uploadLength: 4096,
            CancellationToken.None);

        await registry.RemovePartialAsync(PartialUploadId, CancellationToken.None);

        Assert.Null(await registry.TryGetUploadLengthAsync(PartialUploadId, CancellationToken.None));
        Assert.False(await registry.IsKnownUploadAsync(PartialUploadId, CancellationToken.None));
    }

    [Fact]
    public async Task TryBeginConcatJobAsync_SecondCallerFailsWhileFirstHoldsClaim()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        const string finalFileId = "final-concat-file-id";

        await registry.RegisterFinalConcatAsync(finalFileId, ["partial-a", "partial-b"], CancellationToken.None);

        var firstClaim = await registry.TryBeginConcatJobAsync(finalFileId, CancellationToken.None);
        var secondClaim = await registry.TryBeginConcatJobAsync(finalFileId, CancellationToken.None);

        Assert.True(firstClaim);
        Assert.False(secondClaim);
        Assert.Equal(TusConcatStatus.InProgress, await registry.TryGetConcatStatusAsync(finalFileId, CancellationToken.None));

        await registry.ReleaseConcatRunningLockAsync(finalFileId, CancellationToken.None);
    }

    [Fact]
    public async Task RegisterFinalConcatAsync_ClearsStaleEnqueueMarker()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        const string finalFileId = "stale-enqueue-file-id";

        await registry.RegisterFinalConcatAsync(finalFileId, ["partial-a"], CancellationToken.None);
        Assert.True(await registry.TryAcquireConcatEnqueueSlotAsync(finalFileId, CancellationToken.None));
        Assert.False(await registry.TryAcquireConcatEnqueueSlotAsync(finalFileId, CancellationToken.None));

        await registry.RegisterFinalConcatAsync(finalFileId, ["partial-a"], CancellationToken.None);

        Assert.True(await registry.TryAcquireConcatEnqueueSlotAsync(finalFileId, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterFinalConcatAsync_PreservesExistingConcatStatusOnReplay()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        const string finalFileId = "replay-final-concat-file-id";

        await registry.RegisterFinalConcatAsync(finalFileId, ["partial-a", "partial-b"], CancellationToken.None);
        await registry.MarkConcatCompleteAsync(finalFileId, CancellationToken.None);

        await registry.RegisterFinalConcatAsync(finalFileId, ["partial-a", "partial-b"], CancellationToken.None);

        Assert.Equal(TusConcatStatus.Complete, await registry.TryGetConcatStatusAsync(finalFileId, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterPartialAsync_AllocatesIncreasingPartialIndex()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        var fileTransferId = Guid.Parse(FileTransferId);
        const string firstPartialId = "partial-a";
        const string secondPartialId = "partial-b";

        await registry.RegisterPartialAsync(firstPartialId, fileTransferId, uploadLength: 1024, CancellationToken.None);
        await registry.RegisterPartialAsync(secondPartialId, fileTransferId, uploadLength: 2048, CancellationToken.None);

        var firstPartial = await registry.TryGetPartialInfoAsync(firstPartialId, CancellationToken.None);
        var secondPartial = await registry.TryGetPartialInfoAsync(secondPartialId, CancellationToken.None);

        Assert.NotNull(firstPartial);
        Assert.NotNull(secondPartial);
        Assert.Equal(0, firstPartial.Value.PartialIndex);
        Assert.Equal(1, secondPartial.Value.PartialIndex);
    }

    [Fact]
    public async Task GetPartialUploadsAsync_ListsTheFileTransfersPartialsInCreationOrder()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        var fileTransferId = Guid.Parse(FileTransferId);

        await registry.RegisterPartialAsync("partial-b", fileTransferId, uploadLength: 2048, CancellationToken.None);
        await registry.RegisterPartialAsync("partial-a", fileTransferId, uploadLength: 1024, CancellationToken.None);
        await registry.RegisterPartialAsync("partial-other", Guid.NewGuid(), uploadLength: 512, CancellationToken.None);

        var partialUploads = await registry.GetPartialUploadsAsync(fileTransferId, CancellationToken.None);

        Assert.Equal(
            [new TusPartialUpload("partial-b", 2048), new TusPartialUpload("partial-a", 1024)],
            partialUploads);
    }

    [Fact]
    public async Task GetPartialUploadsAsync_RegisteringAPartialAgain_KeepsItsPlace()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        var fileTransferId = Guid.Parse(FileTransferId);

        await registry.RegisterPartialAsync("partial-a", fileTransferId, uploadLength: 1024, CancellationToken.None);
        await registry.RegisterPartialAsync("partial-b", fileTransferId, uploadLength: 2048, CancellationToken.None);
        await registry.RegisterPartialAsync("partial-a", fileTransferId, uploadLength: 1024, CancellationToken.None);

        var partialUploads = await registry.GetPartialUploadsAsync(fileTransferId, CancellationToken.None);

        Assert.Equal(
            [new TusPartialUpload("partial-a", 1024), new TusPartialUpload("partial-b", 2048)],
            partialUploads);
    }

    [Fact]
    public async Task GetPartialUploadsAsync_LeavesOutRemovedPartials()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();
        var fileTransferId = Guid.Parse(FileTransferId);

        await registry.RegisterPartialAsync("partial-a", fileTransferId, uploadLength: 1024, CancellationToken.None);
        await registry.RegisterPartialAsync("partial-b", fileTransferId, uploadLength: 2048, CancellationToken.None);
        await registry.RemovePartialAsync("partial-a", CancellationToken.None);

        var partialUploads = await registry.GetPartialUploadsAsync(fileTransferId, CancellationToken.None);

        Assert.Equal([new TusPartialUpload("partial-b", 2048)], partialUploads);
    }

    [Fact]
    public async Task GetPartialUploadsAsync_PartialStillInUseAfterADay_IsListed()
    {
        var clock = new ManualClock();
        var registry = new TusPartialUploadRegistry(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions { Clock = clock })));
        var fileTransferId = Guid.Parse(FileTransferId);
        await registry.RegisterPartialAsync("partial-a", fileTransferId, uploadLength: 1024, CancellationToken.None);

        // BrokerTusStore registers the partial again on each request to it
        clock.UtcNow += TimeSpan.FromHours(23);
        await registry.RegisterPartialAsync("partial-a", fileTransferId, uploadLength: 1024, CancellationToken.None);
        clock.UtcNow += TimeSpan.FromHours(2);

        var partialUploads = await registry.GetPartialUploadsAsync(fileTransferId, CancellationToken.None);

        Assert.Equal([new TusPartialUpload("partial-a", 1024)], partialUploads);
    }

    [Fact]
    public async Task GetPartialUploadsAsync_FileTransferWithoutPartials_ReturnsEmptyList()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var registry = scope.CreatePartialUploadRegistry();

        var partialUploads = await registry.GetPartialUploadsAsync(Guid.Parse(FileTransferId), CancellationToken.None);

        Assert.Empty(partialUploads);
    }

    private sealed class ManualClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }
}
