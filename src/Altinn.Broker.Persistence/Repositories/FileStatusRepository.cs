using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class FileStatusRepository : IFileStatusRepository
{
    private DatabaseConnectionProvider _connectionProvider;

    public FileStatusRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task InsertFileStatus(Guid fileId, FileStatus status)
    {
        var connection = await _connectionProvider.GetConnectionAsync();
        using var command = new NpgsqlCommand("", connection);

        command.CommandText =
            "INSERT INTO broker.file_status (file_id_fk, file_status_description_id_fk, file_status_date) " +
            "VALUES (@fileId, @statusId, NOW()) RETURNING file_status_id_pk;";
        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.AddWithValue("@statusId", (int)status);

        var fileStatusId = await command.ExecuteScalarAsync();
        if (fileStatusId == null)
        {
            throw new InvalidOperationException("No file_status_id_pk was returned after insert.");
        }
    }

    public async Task<List<FileStatusEntity>> GetFileStatusHistory(Guid fileId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "SELECT * " +
            "FROM broker.file_status fis " +
            "WHERE fis.file_id_fk = @fileId", connection))
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
                    });
                }
            }
            return fileStatuses;
        }
    }
}
