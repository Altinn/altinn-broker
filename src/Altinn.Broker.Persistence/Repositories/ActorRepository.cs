using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class ActorRepository : IActorRepository
{
    private DatabaseConnectionProvider _connectionProvider;

    public ActorRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ActorEntity?> GetActorAsync(string actorExternalId, CancellationToken cancellationToken)
    {
        using var command = await _connectionProvider.CreateCommand(
        "SELECT actor_id_pk, actor_external_id FROM broker.actor WHERE actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);

        using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        ActorEntity? actor = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            actor = new ActorEntity
            {
                ActorId = reader.GetInt32(reader.GetOrdinal("actor_id_pk")),
                ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
            };
        }
        return actor;

    }

    public async Task<long> AddActorAsync(ActorEntity actor, CancellationToken cancellationToken)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                "INSERT INTO broker.actor (actor_external_id) " +
                "VALUES (@actorExternalId) " +
                "RETURNING actor_id_pk");
        command.Parameters.AddWithValue("@actorExternalId", actor.ActorExternalId);

        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
