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

public class BrokerTusStoreResumeTests
{
    private readonly Mock<ITusStorageResolver> _storageResolver = new();
    private readonly Mock<ITusExpirationDetailsStore> _expirationStore = new();
    private readonly Mock<ITusPartialUploadRegistry> _partialRegistry = new();
    private readonly TusUploadStateRegistry _stateRegistry = new();
    private readonly Mock<ITusUploadProgressCache> _progressCache = new();
    private readonly Mock<ITusUploadActivityCache> _activityCache = new();
    private readonly Mock<ITusConcatCheckpointStore> _concatCheckpointStore = new();
    private readonly DefaultHttpContext _httpContext = new();

    public BrokerTusStoreResumeTests()
    {
        _partialRegistry
            .Setup(x => x.IsPartialAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _partialRegistry
            .Setup(x => x.TryGetPartialInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartialUploadInfo?)null);
        _partialRegistry
            .Setup(x => x.TryGetFileTransferIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        _partialRegistry
            .Setup(x => x.TryGetConcatStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TusConcatStatus?)null);

        _storageResolver
            .Setup(x => x.GetCommittedStagingLengthAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _storageResolver
            .Setup(x => x.GetStagedBlocksLengthAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _storageResolver
            .Setup(x => x.GetDestinationUncommittedBlocksLengthAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _storageResolver
            .Setup(x => x.GetDestinationBlobLengthAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _storageResolver
            .Setup(x => x.TryGetStagedBlocksSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TusStagedBlocksSnapshot?)null);
        _storageResolver
            .Setup(x => x.TryGetDestinationStagedBlocksSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TusStagedBlocksSnapshot?)null);

        _activityCache
            .Setup(x => x.HasRecentActivityAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _expirationStore
            .Setup(x => x.SetExpirationAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task AppendData_TruncatedBody_DoesNotAcceptChunk()
    {
        const string fileId = "11111111-1111-1111-1111-111111111111";
        const long uploadLength = 128L * 1024 * 1024;
        const long committed = 24L * 1024 * 1024;
        var progress = new TusUploadProgressSnapshot(uploadLength, committed, committed, 1);

        _progressCache
            .Setup(x => x.GetAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);
        _partialRegistry
            .Setup(x => x.TryGetUploadLengthAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);
        _storageResolver
            .Setup(x => x.TryGetStagingUploadLengthAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);
        _storageResolver
            .Setup(x => x.HasStagedBlocksAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _storageResolver
            .Setup(x => x.StagingBlobExistsAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _storageResolver
            .Setup(x => x.DestinationBlobExistsAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _httpContext.Request.ContentLength = 64L * 1024 * 1024;
        var store = CreateStore();

        var received = new byte[1024];
        var written = await store.AppendDataAsync(fileId, new MemoryStream(received), CancellationToken.None);

        Assert.Equal(0, written);
        _progressCache.Verify(
            x => x.TryAcceptChunkAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUploadOffset_Head_ReconcilesAbandonedAcceptedOffset()
    {
        const string fileId = "22222222-2222-2222-2222-222222222222";
        const long uploadLength = 128L * 1024 * 1024;
        const long committed = 24L * 64 * 1024 * 1024;
        const long accepted = committed + 62_586_880;
        var wedged = new TusUploadProgressSnapshot(uploadLength, accepted, committed, 25);
        TusUploadProgressSnapshot current = wedged;

        _progressCache
            .Setup(x => x.GetAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);

        _progressCache
            .Setup(x => x.SaveAsync(fileId, It.IsAny<TusUploadProgressSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<string, TusUploadProgressSnapshot, CancellationToken>((_, snapshot, _) => current = snapshot)
            .Returns(Task.CompletedTask);

        _httpContext.Request.Method = HttpMethods.Head;
        var store = CreateStore();

        var offset = await store.GetUploadOffsetAsync(fileId, CancellationToken.None);

        Assert.Equal(committed, offset);
        Assert.Equal(committed, current.AcceptedOffset);
        Assert.Equal(committed, current.CommittedOffset);
        _progressCache.Verify(
            x => x.SaveAsync(
                fileId,
                It.Is<TusUploadProgressSnapshot>(s =>
                    s.AcceptedOffset == committed
                    && s.CommittedOffset == committed),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetUploadOffset_Patch_AfterReconcile_MatchesHeadOffset()
    {
        const string fileId = "33333333-3333-3333-3333-333333333333";
        const long uploadLength = 128L * 1024 * 1024;
        const long committed = 24L * 64 * 1024 * 1024;
        const long accepted = committed + 62_586_880;
        var wedged = new TusUploadProgressSnapshot(uploadLength, accepted, committed, 25);
        var reconciled = new TusUploadProgressSnapshot(uploadLength, committed, committed, 24);

        TusUploadProgressSnapshot? saved = null;
        _progressCache
            .Setup(x => x.GetAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => saved ?? wedged);
        _progressCache
            .Setup(x => x.SaveAsync(fileId, It.IsAny<TusUploadProgressSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<string, TusUploadProgressSnapshot, CancellationToken>((_, snapshot, _) => saved = snapshot)
            .Returns(Task.CompletedTask);

        var store = CreateStore();

        _httpContext.Request.Method = HttpMethods.Head;
        var headOffset = await store.GetUploadOffsetAsync(fileId, CancellationToken.None);

        saved = reconciled;
        _httpContext.Request.Method = HttpMethods.Patch;
        var patchOffset = await store.GetUploadOffsetAsync(fileId, CancellationToken.None);

        Assert.Equal(committed, headOffset);
        Assert.Equal(headOffset, patchOffset);
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
            Options.Create(new TusOptions
            {
                AcceptedOffsetReconcileGracePeriod = TimeSpan.FromMinutes(2)
            }));
    }
}
