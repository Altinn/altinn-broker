using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class FileTransferStatusRepository(NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : IFileTransferStatusRepository
{
    public async Task InsertFileTransferStatus(Guid fileTransferId, FileTransferStatus status, string? detailedFileTransferStatus = null, CancellationToken cancellationToken = default)
    {
        var query = @"
            WITH inserted_status AS (
                INSERT INTO broker.file_transfer_status (
                    file_transfer_id_fk, 
                    file_transfer_status_description_id_fk, 
                    file_transfer_status_date, 
                    file_transfer_status_detailed_description
                )
                VALUES (@fileTransferId, @statusId, NOW(), @detailedFileTransferStatus)
                RETURNING file_transfer_status_id_pk, file_transfer_status_date, file_transfer_status_description_id_fk
            ),
            updated_file_transfer AS (
                UPDATE broker.file_transfer
                SET 
                    latest_file_status_id = inserted_status.file_transfer_status_description_id_fk,
                    latest_file_status_date = inserted_status.file_transfer_status_date
                FROM inserted_status
                WHERE file_transfer.file_transfer_id_pk = @fileTransferId
                    AND (
                        file_transfer.latest_file_status_date IS NULL 
                        OR inserted_status.file_transfer_status_date > file_transfer.latest_file_status_date
                        OR (inserted_status.file_transfer_status_date = file_transfer.latest_file_status_date 
                            AND inserted_status.file_transfer_status_id_pk > COALESCE(
                                (SELECT file_transfer_status_id_pk 
                                 FROM broker.file_transfer_status 
                                 WHERE file_transfer_id_fk = @fileTransferId 
                                   AND file_transfer_status_date = inserted_status.file_transfer_status_date
                                 ORDER BY file_transfer_status_id_pk DESC 
                                 LIMIT 1), 0)
                        )
                    )
            )
            SELECT file_transfer_status_id_pk FROM inserted_status;";

        await using var command = dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@statusId", (int)status);
        command.Parameters.AddWithValue("@detailedFileTransferStatus", detailedFileTransferStatus is null ? DBNull.Value : detailedFileTransferStatus);

        var fileTransferStatusId = await commandExecutor.ExecuteWithRetry(command.ExecuteScalarAsync, cancellationToken);
            
        if (fileTransferStatusId == null)
        {
            throw new InvalidOperationException("No file_transfer_status_id_pk was returned after insert.");
        }
    }

    public async Task<List<FileTransferStatusEntity>> GetFileTransferStatusHistory(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT file_transfer_id_fk, file_transfer_status_description_id_fk, file_transfer_status_date, file_transfer_status_detailed_description " +
            "FROM broker.file_transfer_status fis " +
            "WHERE fis.file_transfer_id_fk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        
        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransferStatuses = new List<FileTransferStatusEntity>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                fileTransferStatuses.Add(new FileTransferStatusEntity()
                {
                    FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                    Status = (FileTransferStatus)reader.GetInt32(reader.GetOrdinal("file_transfer_status_description_id_fk")),
                    Date = reader.GetDateTime(reader.GetOrdinal("file_transfer_status_date")),
                    DetailedStatus = reader.IsDBNull(reader.GetOrdinal("file_transfer_status_detailed_description")) ? null : reader.GetString(reader.GetOrdinal("file_transfer_status_detailed_description"))
                });
            }
            return fileTransferStatuses;
        }, cancellationToken);
    }

    public async Task<List<FileTransferStatusEntity>> GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(FileTransferStatus statusFilter, DateTime minStatusDate, CancellationToken cancellationToken)
    {
        var query = @"
            SELECT file_transfer_id_fk, file_transfer_status_description_id_fk, 
                file_transfer_status_date, file_transfer_status_detailed_description
            FROM broker.file_transfer_status fis
            WHERE fis.file_transfer_status_description_id_fk = @statusFilter
            AND fis.file_transfer_status_date < @minStatusDate
            AND fis.file_transfer_status_date = (
                SELECT MAX(file_transfer_status_date)
                FROM broker.file_transfer_status
                WHERE file_transfer_id_fk = fis.file_transfer_id_fk
            )";

        await using var command = dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("@statusFilter", (int)statusFilter);
        command.Parameters.AddWithValue("@minStatusDate", minStatusDate);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransferStatuses = new List<FileTransferStatusEntity>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                fileTransferStatuses.Add(new FileTransferStatusEntity()
                {
                    FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                    Status = (FileTransferStatus)reader.GetInt32(reader.GetOrdinal("file_transfer_status_description_id_fk")),
                    Date = reader.GetDateTime(reader.GetOrdinal("file_transfer_status_date")),
                    DetailedStatus = reader.IsDBNull(reader.GetOrdinal("file_transfer_status_detailed_description")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("file_transfer_status_detailed_description"))
                });
            }
            return fileTransferStatuses;
        }, cancellationToken);
    }
}
