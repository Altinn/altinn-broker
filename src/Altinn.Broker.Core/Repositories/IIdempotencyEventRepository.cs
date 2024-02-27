namespace Altinn.Broker.Core.Repositories
{
    public interface IIdempotencyEventRepository
    {
        Task AddIdempotencyEventAsync(string id, CancellationToken cancellationToken);
        Task DeleteIdempotencyEventAsync(string id, CancellationToken cancellationToken);
        Task DeleteOldIdempotencyEvents();
    }
}
