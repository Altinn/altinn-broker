using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
internal class ActorFileTransferStatusRepository : IActorFileTransferStatusRepository
{
    private readonly IActorRepository _actorRepository;
    private NpgsqlDataSource _dataSource;

    public ActorFileTransferStatusRepository(IActorRepository actorRepository, NpgsqlDataSource connectionProvider)
    {
        _actorRepository = actorRepository;
        _dataSource = connectionProvider;
    }

    public async Task<List<ActorFileTransferStatusEntity>> GetActorEvents(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
           "SELECT *, a.actor_external_id " +
           "FROM broker.actor_file_transfer_status afs " +
           "INNER JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk " +
           "WHERE afs.file_transfer_id_fk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        var fileTransferStatuses = new List<ActorFileTransferStatusEntity>();
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                fileTransferStatuses.Add(new Core.Domain.ActorFileTransferStatusEntity()
                {
                    FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                    Status = (Core.Domain.Enums.ActorFileTransferStatus)reader.GetInt32(reader.GetOrdinal("actor_file_transfer_status_description_id_fk")),
                    Date = reader.GetDateTime(reader.GetOrdinal("actor_file_transfer_status_date")),
                    Actor = new ActorEntity()
                    {
                        ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk")),
                        ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                    }
                });
            }
        }
        return fileTransferStatuses;
    }

    public async Task InsertActorFileTransferStatus(Guid fileTransferId, ActorFileTransferStatus status, string actorExternalReference, CancellationToken cancellationToken)
    {
        var actor = await _actorRepository.GetActorAsync(actorExternalReference, cancellationToken);
        long actorId;
        if (actor is null)
        {
            actorId = await _actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = actorExternalReference
            }, cancellationToken);
        }
        else
        {
            actorId = actor.ActorId;
        }
        await using var command = _dataSource.CreateCommand(
            "INSERT INTO broker.actor_file_transfer_status (actor_id_fk, file_transfer_id_fk, actor_file_transfer_status_description_id_fk, actor_file_transfer_status_date) " +
            "VALUES (@actorId, @fileTransferId, @actorFileTransferStatusId, NOW())");
        command.Parameters.AddWithValue("@actorId", actorId);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorFileTransferStatusId", (int)status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
