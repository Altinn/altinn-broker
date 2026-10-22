using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using tusdotnet.Models;

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
    public async Task ProcessConcatChainStep_CommitDestination_CommitsStagedStripeBlocks()
    {
        const string fileId = "11111111-1111-1111-1111-111111111111";
        const string partial = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var chunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var chunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);

        _partialRegistry
            .Setup(x => x.TryGetFinalConcatPartialIdsAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([partial]);
        _partialRegistry
            .Setup(x => x.TryGetUploadLengthAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200L);
        _partialRegistry
            .Setup(x => x.TryGetConcatStatusAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TusConcatStatus.InProgress);

        _storageResolver
            .Setup(x => x.GetCommittedStripesAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommittedStripes(0, 0, []));
        _storageResolver
            .Setup(x => x.GetStripeBlobLengthAsync(fileId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);
        _storageResolver
            .Setup(x => x.TryGetStripeStagedBlocksSnapshotAsync(fileId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusStagedBlocksSnapshot(200, [chunk0, chunk1], 2));

        IReadOnlyList<string>? committedBlockIds = null;
        _storageResolver
            .Setup(x => x.CommitStripeBlocksAsync(
                fileId,
                0,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int, IReadOnlyList<string>, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, blockIds, _, _) => committedBlockIds = blockIds.ToArray())
            .Returns(Task.CompletedTask);

        _concatCheckpointStore
            .Setup(x => x.TryGetCheckpointAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusConcatCheckpoint(
                NextStep: TusConcatChainStep.CommitDestination,
                StripeSizeBytes: 200));
        _concatCheckpointStore
            .Setup(x => x.SaveCheckpointAsync(
                fileId,
                It.IsAny<TusConcatCheckpoint>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateStore().ProcessConcatChainStepAsync(fileId, CancellationToken.None);

        Assert.Equal([chunk0, chunk1], committedBlockIds);
        Assert.Equal(TusConcatChainStep.Cleanup, result.NextStep);
    }

    [Fact]
    public async Task ProcessConcatChainStep_PrepareCommit_AdvancesWhenStripeBytesMatch()
    {
        const string fileId = "33333333-3333-3333-3333-333333333333";
        const string partial = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var chunk0 = TusBlockIds.BuildNamespacedBlockId(0, 0);
        var chunk1 = TusBlockIds.BuildNamespacedBlockId(0, 1);

        _partialRegistry
            .Setup(x => x.TryGetFinalConcatPartialIdsAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([partial]);
        _partialRegistry
            .Setup(x => x.TryGetUploadLengthAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200L);
        _partialRegistry
            .Setup(x => x.TryGetConcatStatusAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TusConcatStatus.InProgress);

        _storageResolver
            .Setup(x => x.GetCommittedStripesAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommittedStripes(0, 0, []));
        _storageResolver
            .Setup(x => x.GetStripeBlobLengthAsync(fileId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);
        _storageResolver
            .Setup(x => x.TryGetStripeStagedBlocksSnapshotAsync(fileId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusStagedBlocksSnapshot(200, [chunk0, chunk1], 2));

        TusConcatCheckpoint? saved = null;
        _concatCheckpointStore
            .Setup(x => x.TryGetCheckpointAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusConcatCheckpoint(
                NextStep: TusConcatChainStep.PrepareCommit,
                ValidatedPartialCount: 1,
                TotalValidatedLength: 200,
                StripeSizeBytes: 200));
        _concatCheckpointStore
            .Setup(x => x.SaveCheckpointAsync(
                fileId,
                It.IsAny<TusConcatCheckpoint>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TusConcatCheckpoint, CancellationToken>((_, checkpoint, _) => saved = checkpoint)
            .Returns(Task.CompletedTask);

        var result = await CreateStore().ProcessConcatChainStepAsync(fileId, CancellationToken.None);

        Assert.Equal(TusConcatChainStep.CommitDestination, result.NextStep);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.BlockCount);
        Assert.Equal(200, saved.StagedLength);
        Assert.Equal(1, saved.PreparedStripeCount);
    }

    [Fact]
    public async Task CreateFinalFileAsync_PartialsInCreationOrder_RegistersBoth()
    {
        const string fileId = "44444444-4444-4444-4444-444444444444";
        const string partialEarly = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string partialLate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var fileTransferId = Guid.Parse(fileId);
        _httpContext.Request.Path = $"{TusRouteHelper.TusMapPath}/{fileId}";

        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partialEarly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 100, PartialIndex: 0, BaseOffset: 0));
        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partialLate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 100, PartialIndex: 1, BaseOffset: 100));
        _partialRegistry
            .Setup(x => x.RegisterUploadAsync(fileId, 200L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        IReadOnlyList<string>? registered = null;
        _partialRegistry
            .Setup(x => x.RegisterFinalConcatAsync(
                fileId,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, CancellationToken>((_, partialFileIds, _) => registered = partialFileIds.ToArray())
            .Returns(Task.CompletedTask);

        _progressCache
            .Setup(x => x.GetAsync(partialEarly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusUploadProgressSnapshot(100, 100, 100, 1));
        _progressCache
            .Setup(x => x.GetAsync(partialLate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusUploadProgressSnapshot(100, 100, 100, 1));
        _progressCache
            .Setup(x => x.SaveAsync(fileId, It.IsAny<TusUploadProgressSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storageResolver
            .Setup(x => x.GetStripeSizeAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1024L);
        _storageResolver
            .Setup(x => x.SetDestinationUploadLengthAsync(fileId, 200L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _concatCheckpointStore
            .Setup(x => x.SaveCheckpointAsync(fileId, It.IsAny<TusConcatCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var finalFileId = await CreateStore().CreateFinalFileAsync([partialEarly, partialLate], metadata: string.Empty, CancellationToken.None);

        Assert.Equal(fileId, finalFileId);
        Assert.Equal([partialEarly, partialLate], registered);
    }

    [Fact]
    public async Task CreateFinalFileAsync_RepeatedPartial_DoesNotRegisterConcat()
    {
        const string fileId = "55555555-5555-5555-5555-555555555555";
        const string partial = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var fileTransferId = Guid.Parse(fileId);
        _httpContext.Request.Path = $"{TusRouteHelper.TusMapPath}/{fileId}";

        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partial, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 100, PartialIndex: 0, BaseOffset: 0));
        _progressCache
            .Setup(x => x.GetAsync(partial, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusUploadProgressSnapshot(100, 100, 100, 1));
        _storageResolver
            .Setup(x => x.GetStripeSizeAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1024L);

        var ex = await Assert.ThrowsAsync<TusStoreException>(() =>
            CreateStore().CreateFinalFileAsync([partial, partial], metadata: string.Empty, CancellationToken.None));

        Assert.Contains("order they were created", ex.Message, StringComparison.Ordinal);
        _partialRegistry.Verify(
            x => x.RegisterFinalConcatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFinalFileAsync_GapInBaseOffset_DoesNotRegisterConcat()
    {
        const string fileId = "66666666-6666-6666-6666-666666666666";
        const string partialEarly = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string partialLate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var fileTransferId = Guid.Parse(fileId);
        _httpContext.Request.Path = $"{TusRouteHelper.TusMapPath}/{fileId}";

        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partialEarly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 100, PartialIndex: 0, BaseOffset: 0));
        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(partialLate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartialUploadInfo(fileTransferId, 100, PartialIndex: 1, BaseOffset: 200));
        _progressCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TusUploadProgressSnapshot(100, 100, 100, 1));
        _storageResolver
            .Setup(x => x.GetStripeSizeAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1024L);

        var ex = await Assert.ThrowsAsync<TusStoreException>(() =>
            CreateStore().CreateFinalFileAsync([partialEarly, partialLate], metadata: string.Empty, CancellationToken.None));

        Assert.Contains("missing a partial", ex.Message, StringComparison.Ordinal);
        _partialRegistry.Verify(
            x => x.RegisterFinalConcatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
