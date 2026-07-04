using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusUploadProgressCacheTests
{
    [Fact]
    public async Task TryAcceptChunk_IsAtomicWithoutAffinity()
    {
        var cache = new TusUploadProgressCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<TusUploadProgressCache>.Instance);

        const string fileId = "partial-1";
        await cache.InitializeAsync(fileId, uploadLength: 128 * 1024 * 1024, CancellationToken.None);

        var first = await cache.TryAcceptChunkAsync(fileId, expectedOffset: 0, chunkLength: 32 * 1024 * 1024, CancellationToken.None);
        var conflict = await cache.TryAcceptChunkAsync(fileId, expectedOffset: 0, chunkLength: 32 * 1024 * 1024, CancellationToken.None);
        var second = await cache.TryAcceptChunkAsync(
            fileId,
            expectedOffset: first.NewAcceptedOffset,
            chunkLength: 32 * 1024 * 1024,
            CancellationToken.None);

        Assert.Equal(TusAcceptChunkStatus.Accepted, first.Status);
        Assert.Equal(0, first.BlockIndex);
        Assert.Equal(TusAcceptChunkStatus.Conflict, conflict.Status);
        Assert.Equal(first.NewAcceptedOffset, conflict.CurrentAcceptedOffset);
        Assert.Equal(TusAcceptChunkStatus.Accepted, second.Status);
        Assert.Equal(1, second.BlockIndex);

        var progress = await cache.GetAsync(fileId, CancellationToken.None);
        Assert.NotNull(progress);
        Assert.Equal(64L * 1024 * 1024, progress!.AcceptedOffset);
        Assert.Equal(2, progress.NextBlockIndex);
    }

    [Fact]
    public async Task IncrementCommittedOffset_UpdatesProgress()
    {
        var cache = new TusUploadProgressCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<TusUploadProgressCache>.Instance);

        const string fileId = "partial-2";
        await cache.InitializeAsync(fileId, uploadLength: 64 * 1024 * 1024, CancellationToken.None);
        var accepted = await cache.TryAcceptChunkAsync(fileId, 0, 16 * 1024 * 1024, CancellationToken.None);
        Assert.Equal(TusAcceptChunkStatus.Accepted, accepted.Status);

        await cache.IncrementCommittedOffsetAsync(fileId, 16 * 1024 * 1024, CancellationToken.None);

        var progress = await cache.GetAsync(fileId, CancellationToken.None);
        Assert.NotNull(progress);
        Assert.Equal(16L * 1024 * 1024, progress!.CommittedOffset);
    }
}
