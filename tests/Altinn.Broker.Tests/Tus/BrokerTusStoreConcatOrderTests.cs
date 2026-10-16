using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xtensible.TusDotNet.Azure;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class BrokerTusStoreConcatOrderTests
{
    private readonly Mock<ITusStorageResolver> _storageResolver = new();
    private readonly Mock<ITusExpirationDetailsStore> _expirationStore = new();
    private readonly Mock<ITusPartialUploadRegistry> _partialRegistry = new();
    private readonly TusUploadStateRegistry _stateRegistry = new();
    private readonly Mock<ITusUploadProgressCache> _progressCache = new();
    private readonly Mock<ITusUploadActivityCache> _activityCache = new();
    private readonly Mock<ITusConcatCheckpointStore> _concatCheckpointStore = new();
    private readonly DefaultHttpContext _httpContext = new();

    [Fact]
    public async Task ProcessConcatChainStep_CommitDestination_CommitsBlocksInUploadConcatOrder()
    {
        const string fileId = "11111111-1111-1111-1111-111111111111";
        const string partialEarly = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string partialLate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var fileTransferId = Guid.Parse(fileId);

        var earlyChunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var earlyChunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);
        var lateChunk0 = TusBlockIds.BuildNamespacedBlockId(1, 0);
        var creationOrder = new[] { earlyChunk0, earlyChunk1, lateChunk0 };

        _partialRegistry
            .Setup(x => x.TryGetFinalConcatPartialIdsAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([partialLate, partialEarly]);
        _partialRegistry
            .Setup(x => x.TryGetUploadLengthAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(300L);
        _partialRegistry
            .Setup(x => x.TryGetConcatStatusAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TusConcatStatus.InProgress);
        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partialEarly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 200, PartialIndex: 0));
        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partialLate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 100, PartialIndex: 1));
        _partialRegistry
            .Setup(x => x.RegisterUploadAsync(fileId, 300L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storageResolver
            .Setup(x => x.GetDestinationBlobLengthAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);
        _storageResolver
            .Setup(x => x.SetStagingUploadLengthAsync(fileId, 300L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storageResolver
            .Setup(x => x.TryGetDestinationStagedBlocksSnapshotAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusStagedBlocksSnapshot(300, creationOrder, creationOrder.Length));

        IReadOnlyList<string>? committedBlockIds = null;
        _storageResolver
            .Setup(x => x.CommitBlocksToDestinationAsync(
                fileId,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, CancellationToken>((_, blockIds, _) => committedBlockIds = blockIds.ToArray())
            .Returns(Task.CompletedTask);

        _concatCheckpointStore
            .Setup(x => x.TryGetCheckpointAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusConcatCheckpoint(NextStep: TusConcatChainStep.CommitDestination));
        _concatCheckpointStore
            .Setup(x => x.SaveCheckpointAsync(
                fileId,
                It.IsAny<TusConcatCheckpoint>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = CreateStore();

        var result = await store.ProcessConcatChainStepAsync(fileId, CancellationToken.None);

        Assert.Equal([lateChunk0, earlyChunk0, earlyChunk1], committedBlockIds);
        Assert.Equal(TusConcatChainStep.Cleanup, result.NextStep);
    }

    private BrokerTusStore CreateStore()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = _httpContext };
        return new BrokerTusStore(
            _storageResolver.Object,
            _expirationStore.Object,
            _partialRegistry.Object,
            _stateRegistry,
            _progressCache.Object,
            _activityCache.Object,
            _concatCheckpointStore.Object,
            httpContextAccessor,
            NullLogger<BrokerTusStore>.Instance,
            Options.Create(new AzureStorageOptions { ConcurrentUploadThreads = 1 }),
            Options.Create(new TusOptions()));
    }
}
