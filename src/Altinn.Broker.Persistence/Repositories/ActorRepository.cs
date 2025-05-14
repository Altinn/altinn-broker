using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class ActorRepository(NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : IActorRepository
{
    public async Task<ActorEntity?> GetActorAsync(string actorExternalId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
        "SELECT actor_id_pk, actor_external_id FROM broker.actor WHERE actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            ActorEntity? actor = null;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                actor = new ActorEntity
                {
                    ActorId = reader.GetInt32(reader.GetOrdinal("actor_id_pk")),
                    ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                };
            }
            return actor;
        }, cancellationToken);
    }

    public async Task<long> AddActorAsync(ActorEntity actor, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
                "INSERT INTO broker.actor (actor_external_id) " +
                "VALUES (@actorExternalId) " +
                "RETURNING actor_id_pk");
        command.Parameters.AddWithValue("@actorExternalId", actor.ActorExternalId);

        return await commandExecutor.ExecuteWithRetry(async (ct) => 
            (long)(await command.ExecuteScalarAsync(ct))!, 
            cancellationToken);
    }
}
