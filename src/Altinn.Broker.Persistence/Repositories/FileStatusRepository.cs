using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

namespace Altinn.Broker.Persistence.Repositories;
public class FileStatusRepository : IFileStatusRepository
{
    private DatabaseConnectionProvider _connectionProvider;

    public FileStatusRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task InsertFileStatus(Guid fileId, FileStatus status, string? detailedFileStatus = null)
    {
        using var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.file_status (file_id_fk, file_status_description_id_fk, file_status_date, file_status_detailed_description) " +
            "VALUES (@fileId, @statusId, NOW(), @detailedFileStatus) RETURNING file_status_id_pk;");
        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.AddWithValue("@statusId", (int)status);
        command.Parameters.AddWithValue("@detailedFileStatus", detailedFileStatus is null ? DBNull.Value : detailedFileStatus);

        var fileStatusId = await command.ExecuteScalarAsync();
        if (fileStatusId == null)
        {
            throw new InvalidOperationException("No file_status_id_pk was returned after insert.");
        }
    }

    public async Task<List<FileStatusEntity>> GetFileStatusHistory(Guid fileId)
    {

        using (var command = await _connectionProvider.CreateCommand(
            "SELECT file_id_fk, file_status_description_id_fk, file_status_date, file_status_detailed_description " +
            "FROM broker.file_status fis " +
            "WHERE fis.file_id_fk = @fileId"))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            var fileStatuses = new List<FileStatusEntity>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    fileStatuses.Add(new FileStatusEntity()
                    {
                        FileId = reader.GetGuid(reader.GetOrdinal("file_id_fk")),
                        Status = (FileStatus)reader.GetInt32(reader.GetOrdinal("file_status_description_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("file_status_date")),
                        DetailedStatus = reader.IsDBNull(reader.GetOrdinal("file_status_detailed_description")) ? null : reader.GetString(reader.GetOrdinal("file_status_detailed_description"))
                    });
                }
            }
            return fileStatuses;
        }
    }
}
