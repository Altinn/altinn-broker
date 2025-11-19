using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
internal class ActorFileTransferStatusRepository(IActorRepository actorRepository, NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : IActorFileTransferStatusRepository
{
    public async Task<List<ActorFileTransferStatusEntity>> GetActorEvents(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
           "SELECT *, a.actor_external_id " +
           "FROM broker.actor_file_transfer_status afs " +
           "INNER JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk " +
           "WHERE afs.file_transfer_id_fk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        
        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransferStatuses = new List<ActorFileTransferStatusEntity>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
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
            return fileTransferStatuses;
        }, cancellationToken);
    }

    public async Task InsertActorFileTransferStatus(Guid fileTransferId, ActorFileTransferStatus status, string actorExternalReference, CancellationToken cancellationToken)
    {
        var actor = await actorRepository.GetActorAsync(actorExternalReference, cancellationToken);
        long actorId;
        if (actor is null)
        {
            actorId = await actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = actorExternalReference
            }, cancellationToken);
        }
        else
        {
            actorId = actor.ActorId;
        }
        
        // This query performs two operations atomically:
        // 1. Inserts a new actor file transfer status record into the history table
        // 2. Updates the denormalized latest_status table to keep the most recent status per (file_transfer, actor) pair
        //
        // The denormalization logic:
        // - If no latest status exists for this (file_transfer, actor) pair, insert it
        // - If a latest status exists, update it only if the new status is "newer":
        //   * Newer by timestamp (new_date > old_date), OR
        //   * Same timestamp but higher ID (handles edge case of simultaneous inserts)
        //
        // The WHERE clause in ON CONFLICT ensures we only update when the new status is actually newer,
        // preventing race conditions where an older status might overwrite a newer one.
        var query = @"
            WITH inserted_status AS (
                INSERT INTO broker.actor_file_transfer_status (
                    actor_id_fk, 
                    file_transfer_id_fk, 
                    actor_file_transfer_status_description_id_fk, 
                    actor_file_transfer_status_date
                )
                VALUES (@actorId, @fileTransferId, @actorFileTransferStatusId, NOW())
                RETURNING actor_file_transfer_status_id_pk, actor_file_transfer_status_date, actor_file_transfer_status_description_id_fk, actor_id_fk, file_transfer_id_fk
            )
            INSERT INTO broker.actor_file_transfer_latest_status (
                file_transfer_id_fk,
                actor_id_fk,
                latest_actor_status_id,
                latest_actor_status_date
            )
            SELECT 
                inserted_status.file_transfer_id_fk,
                inserted_status.actor_id_fk,
                inserted_status.actor_file_transfer_status_description_id_fk,
                inserted_status.actor_file_transfer_status_date
            FROM inserted_status
            ON CONFLICT (file_transfer_id_fk, actor_id_fk) 
            DO UPDATE SET
                latest_actor_status_id = EXCLUDED.latest_actor_status_id,
                latest_actor_status_date = EXCLUDED.latest_actor_status_date
            WHERE 
                -- Update if new status has a newer timestamp
                EXCLUDED.latest_actor_status_date > actor_file_transfer_latest_status.latest_actor_status_date
                OR 
                -- Or if same timestamp, update only if new status has higher ID (tie-breaker for simultaneous inserts)
                (EXCLUDED.latest_actor_status_date = actor_file_transfer_latest_status.latest_actor_status_date
                    AND EXCLUDED.latest_actor_status_id > actor_file_transfer_latest_status.latest_actor_status_id);";

        await using var command = dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("@actorId", actorId);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorFileTransferStatusId", (int)status);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
}
