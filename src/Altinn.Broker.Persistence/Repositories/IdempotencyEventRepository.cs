using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class IdempotencyEventRepository : IIdempotencyEventRepository
{
    private DatabaseConnectionProvider _connectionProvider;

    public IdempotencyEventRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }


    public async Task AddIdempotencyEventAsync(string IdempotencyEventId, CancellationToken cancellationToken)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "INSERT INTO broker.idempotency_event (idempotency_event_id_pk, created)" +
                    "VALUES (@idempotency_event_id_pk, @created) ");
        command.Parameters.AddWithValue("@idempotency_event_id_pk", IdempotencyEventId);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task DeleteIdempotencyEventAsync(string IdempotencyEventId, CancellationToken cancellationToken)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "DELETE FROM broker.idempotency_event " +
                    "WHERE idempotency_event_id_pk = @idempotency_event_id_pk");
        command.Parameters.AddWithValue("@idempotency_event_id_pk", IdempotencyEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task DeleteOldIdempotencyEvents()
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "DELETE FROM broker.idempotency_event " +
                    "WHERE created < @created");

        command.Parameters.AddWithValue("@created", DateTime.UtcNow.AddDays(-1));

        await command.ExecuteNonQueryAsync();
    }
}
