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

    public async Task<Actor?> GetActorAsync(long actorId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using var command = new NpgsqlCommand(
            "SELECT * FROM broker.actor WHERE actor_id_pk = @actorId",
            connection);

        command.Parameters.AddWithValue("@actorId", actorId);

        using NpgsqlDataReader reader = command.ExecuteReader();

        Actor? actor = null;

        while (reader.Read())
        {
            actor = new Actor
            {
                ActorId = reader.GetInt32(reader.GetOrdinal("actor_id_pk")),
                ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
            };
        }

        return actor;
    }

    public async Task AddActorAsync(Actor actor)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        NpgsqlCommand command = new NpgsqlCommand(
                    "INSERT INTO broker.actor (actor_id_pk, actor_external_id) " +
                    "VALUES (@actorId, @actorExternalId)",
                    connection);

        command.Parameters.AddWithValue("@actorId", actor.ActorId);
        command.Parameters.AddWithValue("@actorExternalId", actor.ActorExternalId);
        command.ExecuteNonQuery();
    }
}