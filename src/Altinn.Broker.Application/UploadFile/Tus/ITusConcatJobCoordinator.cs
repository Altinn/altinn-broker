namespace Altinn.Broker.Application.UploadFile.Tus;

public enum TusConcatJobPhase
{
    Pending,
    InProgress,
    Complete
}

public interface ITusConcatJobCoordinator
{
    Task<TusConcatJobPhase?> GetConcatPhaseAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsConcatCompleteAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> TryAcquireEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> TryBeginJobAsync(string tusFileId, CancellationToken cancellationToken);

    Task ReleaseRunningLockAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> TryAcquirePublishEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsConcatRunningAsync(string tusFileId, CancellationToken cancellationToken);

    Task ClearConcatEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken);

    Task ClearPublishEnqueueSlotAsync(string tusFileId, CancellationToken cancellationToken);
}
