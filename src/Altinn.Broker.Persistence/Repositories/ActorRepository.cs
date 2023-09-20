using Altinn.Broker.Core.Domain;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ActorRepository
{
    private readonly string _connectionString;

    public ActorRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Actor? GetActor(int actorId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

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

    public void SaveActor(Actor actor)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        // Here's a simple method to determine if the Actor exists based on its primary key
        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM broker.actor WHERE actor_id_pk = @actorId", connection))
        {
            checkCommand.Parameters.AddWithValue("@actorId", actor.ActorId);
            long existingCount = (long)checkCommand.ExecuteScalar();

            NpgsqlCommand command;

            if (existingCount == 0)
            {
                // Insert new Actor
                command = new NpgsqlCommand(
                    "INSERT INTO broker.actor (actor_id_pk, actor_external_id) " +
                    "VALUES (@actorId, @actorExternalId)",
                    connection);
            }
            else
            {
                // Update existing Actor
                command = new NpgsqlCommand(
                    "UPDATE broker.actor " +
                    "SET actor_external_id = @actorExternalId " +
                    "WHERE actor_id_pk = @actorId",
                    connection);
            }

            command.Parameters.AddWithValue("@actorId", actor.ActorId);
            command.Parameters.AddWithValue("@actorExternalId", actor.ActorExternalId);

            command.ExecuteNonQuery();
        }
    }
}
