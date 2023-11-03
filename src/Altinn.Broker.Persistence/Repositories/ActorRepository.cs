﻿using Altinn.Broker.Core.Domain;
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

    public async Task<Actor?> GetActorAsync(string actorExternalId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using var command = new NpgsqlCommand(
            "SELECT * FROM broker.actor WHERE actor_external_id = @actorExternalId",
            connection);

        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);

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

    public async Task<long> AddActorAsync(Actor actor)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        NpgsqlCommand command = new NpgsqlCommand(
                    "INSERT INTO broker.actor (actor_external_id) " +
                    "VALUES (@actorExternalId) " +
                    "RETURNING actor_id_pk",
                    connection);

        command.Parameters.AddWithValue("@actorExternalId", actor.ActorExternalId);
        return (long)command.ExecuteScalar()!;
    }
}
