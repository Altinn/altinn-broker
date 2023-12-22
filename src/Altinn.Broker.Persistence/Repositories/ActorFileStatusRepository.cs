using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

namespace Altinn.Broker.Persistence.Repositories;
internal class ActorFileStatusRepository : IActorFileStatusRepository
{
    private readonly IActorRepository _actorRepository;
    private DatabaseConnectionProvider _connectionProvider;

    public ActorFileStatusRepository(IActorRepository actorRepository, DatabaseConnectionProvider connectionProvider)
    {
        _actorRepository = actorRepository;
        _connectionProvider = connectionProvider;
    }

    public async Task<List<ActorFileStatusEntity>> GetActorEvents(Guid fileId)
    {

        await using (var command = await _connectionProvider.CreateCommand(
            "SELECT *, a.actor_external_id " +
            "FROM broker.actor_file_status afs " +
            "INNER JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk " +
            "WHERE afs.file_id_fk = @fileId"))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            var fileStatuses = new List<ActorFileStatusEntity>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    fileStatuses.Add(new Core.Domain.ActorFileStatusEntity()
                    {
                        FileId = reader.GetGuid(reader.GetOrdinal("file_id_fk")),
                        Status = (Core.Domain.Enums.ActorFileStatus)reader.GetInt32(reader.GetOrdinal("actor_file_status_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("actor_file_status_date")),
                        Actor = new ActorEntity()
                        {
                            ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk")),
                            ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                        }
                    });
                }
            }
            return fileStatuses;
        }
    }

    public async Task InsertActorFileStatus(Guid fileId, ActorFileStatus status, string actorExternalReference)
    {
        var actor = await _actorRepository.GetActorAsync(actorExternalReference);
        long actorId = 0;
        if (actor is null)
        {
            actorId = await _actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = actorExternalReference
            });
        }
        else
        {
            actorId = actor.ActorId;
        }

        await using (var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.actor_file_status (actor_id_fk, file_id_fk, actor_file_status_id_fk, actor_file_status_date) " +
            "VALUES (@actorId, @fileId, @actorFileStatusId, NOW())"))
        {
            command.Parameters.AddWithValue("@actorId", actorId);
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@actorFileStatusId", (int)status);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }
    }
}
