namespace Altinn.Broker.Core.Services;

public interface ITusUploadActivityCache
{
    Task RecordActivityAsync(Guid fileTransferId, CancellationToken cancellationToken = default);

    Task<bool> HasRecentActivityAsync(Guid fileTransferId, TimeSpan window, CancellationToken cancellationToken = default);
}
