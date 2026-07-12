using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Integrations.Tus;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusConcatCheckpointStoreTests
{
    [Fact]
    public async Task SaveAndLoadCheckpoint_RoundTrips()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var store = new TusConcatCheckpointStore(scope.DistributedCache);
        const string tusFileId = "concat-checkpoint-test";

        var checkpoint = new TusConcatCheckpoint(
            NextStep: TusConcatChainStep.PrepareCommit,
            ValidatedPartialCount: 2,
            TotalValidatedLength: 2048,
            BlockCount: 16,
            StagedLength: 2048);

        await store.SaveCheckpointAsync(tusFileId, checkpoint, CancellationToken.None);
        var loaded = await store.TryGetCheckpointAsync(tusFileId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(TusConcatChainStep.PrepareCommit, loaded!.NextStep);
        Assert.Equal(2, loaded.ValidatedPartialCount);
        Assert.Equal(2048, loaded.TotalValidatedLength);
        Assert.Equal(16, loaded.BlockCount);
        Assert.Equal(2048, loaded.StagedLength);
    }

    [Fact]
    public async Task ClearCheckpoint_RemovesSavedState()
    {
        await using var scope = TestHybridCacheFactory.CreateScope();
        var store = new TusConcatCheckpointStore(scope.DistributedCache);
        const string tusFileId = "concat-checkpoint-clear-test";

        await store.SaveCheckpointAsync(
            tusFileId,
            new TusConcatCheckpoint(NextStep: TusConcatChainStep.CommitDestination),
            CancellationToken.None);
        await store.ClearCheckpointAsync(tusFileId, CancellationToken.None);

        Assert.Null(await store.TryGetCheckpointAsync(tusFileId, CancellationToken.None));
    }
}
