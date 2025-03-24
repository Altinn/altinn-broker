using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class FileTransferStatusRepository(NpgsqlDataSource dataSource) : IFileTransferStatusRepository
{
    public async Task InsertFileTransferStatus(Guid fileTransferId, FileTransferStatus status, string? detailedFileTransferStatus = null, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
                    "INSERT INTO broker.file_transfer_status (file_transfer_id_fk, file_transfer_status_description_id_fk, file_transfer_status_date, file_transfer_status_detailed_description) " +
                    "VALUES (@fileTransferId, @statusId, NOW(), @detailedFileTransferStatus) RETURNING file_transfer_status_id_pk;");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@statusId", (int)status);
        command.Parameters.AddWithValue("@detailedFileTransferStatus", detailedFileTransferStatus is null ? DBNull.Value : detailedFileTransferStatus);

        var fileTransferStatusId = await command.ExecuteScalarAsync(cancellationToken);
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
        var fileTransferStatuses = new List<FileTransferStatusEntity>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                fileTransferStatuses.Add(new FileTransferStatusEntity()
                {
                    FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                    Status = (FileTransferStatus)reader.GetInt32(reader.GetOrdinal("file_transfer_status_description_id_fk")),
                    Date = reader.GetDateTime(reader.GetOrdinal("file_transfer_status_date")),
                    DetailedStatus = reader.IsDBNull(reader.GetOrdinal("file_transfer_status_detailed_description")) ? null : reader.GetString(reader.GetOrdinal("file_transfer_status_detailed_description"))
                });
            }
        }
        return fileTransferStatuses;
    }

    public async Task<List<FileTransferStatusEntity>> GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(FileTransferStatus statusFilter, DateTime minStatusAge, CancellationToken cancellationToken)
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
        command.Parameters.AddWithValue("@minStatusDate", DateTime.UtcNow.Subtract(minStatusAge));

        var fileTransferStatuses = new List<FileTransferStatusEntity>();

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
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
        }

        return fileTransferStatuses;
    }
}
