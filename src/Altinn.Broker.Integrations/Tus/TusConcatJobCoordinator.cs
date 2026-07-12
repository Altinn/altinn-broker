using Altinn.Broker.Application.UploadFile;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusConcatJobCoordinator(ITusPartialUploadRegistry partialUploadRegistry) : ITusConcatJobCoordinator
{
    public async Task<TusConcatJobPhase?> GetConcatPhaseAsync(string tusFileId, CancellationToken cancellationToken)
    {
        var status = await partialUploadRegistry.TryGetConcatStatusAsync(tusFileId, cancellationToken);
        return status switch
        {
            TusConcatStatus.Pending => TusConcatJobPhase.Pending,
            TusConcatStatus.InProgress => TusConcatJobPhase.InProgress,
            TusConcatStatus.Complete => TusConcatJobPhase.Complete,
            _ => null
        };
    }

    public async Task<bool> IsConcatCompleteAsync(string tusFileId, CancellationToken cancellationToken)
        => await partialUploadRegistry.TryGetConcatStatusAsync(tusFileId, cancellationToken) == TusConcatStatus.Complete;

    public Task<bool> TryAcquireEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.TryAcquireConcatEnqueueSlotAsync(tusFileId, cancellationToken);

    public Task<bool> TryBeginJobAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.TryBeginConcatJobAsync(tusFileId, cancellationToken);

    public Task ReleaseRunningLockAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.ReleaseConcatRunningLockAsync(tusFileId, cancellationToken);

    public Task<bool> TryAcquirePublishEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.TryAcquirePublishEnqueueSlotAsync(tusFileId, cancellationToken);

    public Task<bool> IsConcatRunningAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.IsConcatRunningAsync(tusFileId, cancellationToken);

    public Task ClearConcatEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.ClearConcatEnqueueSlotAsync(tusFileId, cancellationToken);

    public Task ClearPublishEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.ClearPublishEnqueueSlotAsync(tusFileId, cancellationToken);
}
