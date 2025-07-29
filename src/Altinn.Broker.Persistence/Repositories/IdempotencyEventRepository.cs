using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class IdempotencyEventRepository(NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : IIdempotencyEventRepository
{
    public async Task AddIdempotencyEventAsync(string IdempotencyEventId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
                    "INSERT INTO broker.idempotency_event (idempotency_event_id_pk, created)" +
                    "VALUES (@idempotency_event_id_pk, @created) ");
        command.Parameters.AddWithValue("@idempotency_event_id_pk", IdempotencyEventId);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
}
