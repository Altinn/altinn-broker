namespace Altinn.Broker.Core.Repositories;

public interface IIdempotencyEventRepository
{
    Task AddIdempotencyEventAsync(string id, CancellationToken cancellationToken);
}
