using System.Data;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Broker.Persistence.Repositories;

public class FileRepository : IFileRepository
{
    private DatabaseConnectionProvider _connectionProvider;
    private readonly IActorRepository _actorRepository;

    public FileRepository(DatabaseConnectionProvider connectionProvider, IActorRepository actorRepository)
    {
        _connectionProvider = connectionProvider;
        _actorRepository = actorRepository;
    }

    public async Task<FileEntity?> GetFileAsync(Guid fileId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        var file = new FileEntity();
        using var command = new NpgsqlCommand(
            "SELECT *, sr.file_location, afs.actor_id_fk as eventActorId, a.actor_external_id as eventActorExernalReference, afs.actor_file_status_id_fk as eventActorFileStatusId, afs.actor_file_status_date as eventActorFileStatusDate, sender.actor_external_id as senderExternalId, " +
                "(" +
                    "SELECT fs.file_status_description_id_fk " +
                    "FROM broker.file_status fs " +
                    "WHERE fs.file_id_fk = @fileId " +
                    "ORDER BY fs.file_status_date desc " +
                    "LIMIT 1" +
                ") as file_status_description_id_fk, " +
                "(" +
                    "SELECT fs.file_status_date " +
                    "FROM broker.file_status fs " +
                    "WHERE fs.file_id_fk = @fileId " +
                    "ORDER BY fs.file_status_date desc " +
                    "LIMIT 1" +
                ") as file_status_date " +
            "FROM broker.file " +
            "LEFT JOIN broker.storage_reference sr on sr.storage_reference_id_pk = storage_reference_id_fk " +
            "LEFT JOIN broker.actor_file_status afs on afs.file_id_fk = file_id_pk " +
            "LEFT JOIN broker.actor sender on sender.actor_id_pk = sender_actor_id_fk " +
            "LEFT JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk " +
            "WHERE file_id_pk = @fileId",
            connection);
        { 
            command.Parameters.AddWithValue("@fileId", fileId);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                file = new FileEntity
                {
                    FileId = reader.GetGuid(reader.GetOrdinal("file_id_pk")),
                    ApplicationId = reader.GetString(reader.GetOrdinal("application_id")),
                    Filename = reader.GetString(reader.GetOrdinal("filename")),
                    Checksum = reader.GetString(reader.GetOrdinal("checksum")),
                    ExternalFileReference = reader.GetString(reader.GetOrdinal("external_file_reference")),
                    FileStatus = (FileStatus)reader.GetInt32(reader.GetOrdinal("file_status_description_id_fk")),
                    FileStatusChanged = reader.GetDateTime(reader.GetOrdinal("file_status_date")),
                    Uploaded = reader.GetDateTime(reader.GetOrdinal("uploaded")),
                    FileLocation = reader.GetString(reader.GetOrdinal("file_location")),
                    Sender = reader.GetString(reader.GetOrdinal("senderExternalId"))
                };
                var receipts = new List<ActorFileStatusEntity>();
                if (!reader.IsDBNull(reader.GetOrdinal("actor_id_fk")))
                {
                    do
                    {
                        receipts.Add(new ActorFileStatusEntity()
                        {
                            FileId = fileId,
                            Actor = new ActorEntity()
                            {
                                ActorId = reader.GetInt64(reader.GetOrdinal("eventActorId")),
                                ActorExternalId = reader.GetString(reader.GetOrdinal("eventActorExernalReference"))
                            },
                            Status = (ActorFileStatus)reader.GetInt32(reader.GetOrdinal("eventActorFileStatusId")),
                            Date = reader.GetDateTime(reader.GetOrdinal("eventActorFileStatusDate"))
                        });
                    } while (reader.Read());
                }
                file.ActorEvents = receipts;
            }
            else
            {
                return null;
            }
        }
        file.Metadata = await GetMetadata(fileId);
        return file;
    }

    public async Task AddReceiptAsync(ActorFileStatusEntity receipt)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        var actorId = receipt.Actor.ActorId;
        if (actorId == 0)
        {
            actorId = await _actorRepository.AddActorAsync(receipt.Actor);
        }

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.actor_file_status (actor_id_fk, file_id_fk, actor_file_status_id_fk, actor_file_status_date) " +
            "VALUES (@actorId, @fileId, @actorFileStatusId, NOW())", connection))
        {
            command.Parameters.AddWithValue("@actorId", actorId);
            command.Parameters.AddWithValue("@fileId", receipt.FileId);
            command.Parameters.AddWithValue("@actorFileStatusId", (int)receipt.Status);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }
    }

    public async Task<Guid> AddFileAsync(FileEntity file, string caller)
    {
        long actorId;
        var actor = await _actorRepository.GetActorAsync(file.Sender);
        if (actor is null)
        {
            actorId = await _actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = file.Sender
            });
        }
        else
        {
            actorId = actor.ActorId;
        }

        var connection = await _connectionProvider.GetConnectionAsync();
        var fileId = Guid.NewGuid();
        NpgsqlCommand command = new NpgsqlCommand(
            "INSERT INTO broker.file (file_id_pk, application_id, filename, checksum, external_file_reference, sender_actor_id_fk, uploaded, storage_reference_id_fk) " +
            "VALUES (@fileId, @applicationId, @filename, @checksum, @externalFileReference, @senderActorId, @uploaded, @storageReferenceId)",
            connection);
        long storageReference = await AddFileStorageReferenceAsync(file.FileLocation);

        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.AddWithValue("@applicationId", caller);
        command.Parameters.AddWithValue("@filename", file.Filename);
        command.Parameters.AddWithValue("@checksum", file.Checksum);
        command.Parameters.AddWithValue("@senderActorId", actorId);
        command.Parameters.AddWithValue("@externalFileReference", file.ExternalFileReference);
        command.Parameters.AddWithValue("@fileStatusId", (int)file.FileStatus);
        command.Parameters.AddWithValue("@uploaded", DateTime.UtcNow);
        command.Parameters.AddWithValue("@storageReferenceId", storageReference);

        command.ExecuteNonQuery();

        var addActorTasks = file.ActorEvents.Select(actorEvent => AddReceipt(fileId, ActorFileStatus.Initialized, actorEvent.Actor.ActorExternalId));

        try
        {
            await Task.WhenAll(addActorTasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        await SetMetadata(fileId, file.Metadata);
        await InsertFileStatus(fileId, FileStatus.Initialized);

        return fileId;
    }

    private async Task<long> AddFileStorageReferenceAsync(string fileLocation)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        long? storageReferenceId;
        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.storage_reference (storage_reference_id_pk, file_location) " +
            "VALUES (DEFAULT, @fileLocation) " +
            "RETURNING storage_reference_id_pk", connection))
        {
            command.Parameters.AddWithValue("@fileLocation", fileLocation ?? "altinn-3-blob");
            storageReferenceId = (long)command.ExecuteScalar();
            return (long)storageReferenceId;
        }
    }

    public async Task<List<Guid>> GetFilesAvailableForCaller(string actorExernalReference)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "SELECT DISTINCT afs.file_id_fk, 'Recipient'  " +
            "FROM broker.actor_file_status afs " +
            "WHERE afs.actor_id_fk = (  " +
            "    SELECT a.actor_id_pk  " +
            "    FROM broker.actor a  " +
            "    WHERE a.actor_external_id = @actorExternalId" +
            ")" +
            "UNION " +
            "SELECT f.file_id_pk, 'Sender' " +
            "FROM broker.file f " +
            "INNER JOIN broker.actor a on a.actor_id_pk = f.sender_actor_id_fk " +
            "WHERE a.actor_external_id = @actorExternalId " +
            "UNION " +
            "SELECT f.file_id_pk, 'Service' " +
            "FROM broker.file f " +
            "WHERE f.application_id = @actorExternalId", connection))
        {
            command.Parameters.AddWithValue("@actorExternalid", actorExernalReference);

            var files = new List<Guid>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var fileId = reader.GetGuid(0);
                    files.Add(fileId);
                }
            }
            return files;
        }
    }

    public async Task AddReceipt(Guid fileId, Core.Domain.Enums.ActorFileStatus status, string actorExternalReference)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

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

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.actor_file_status (actor_id_fk, file_id_fk, actor_file_status_id_fk, actor_file_status_date) " +
            "VALUES (@actorId, @fileId, @actorFileStatusId, NOW())", connection))
        {
            command.Parameters.AddWithValue("@actorId", actorId);
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@actorFileStatusId", (int)status);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }
    }

    public async Task SetStorageReference(Guid fileId, string storageReference)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "UPDATE broker.storage_reference " +
            "SET file_location = @fileLocation " +
            "WHERE storage_reference_id_pk = ( " +
                "SELECT f.storage_reference_id_fk " +
                "FROM broker.file f " +
                "WHERE f.file_id_pk = @fileId" +
            ")", connection))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@fileLocation", storageReference);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }
    }

    public async Task<List<FileStatusEntity>> GetFileStatusHistoryAsync(Guid fileId)
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

    public async Task<List<ActorFileStatusEntity>> GetActorEvents(Guid fileId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "SELECT *, a.actor_external_id " +
            "FROM broker.actor_file_status afs " +
            "INNER JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk " +
            "WHERE afs.file_id_fk = @fileId", connection))
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

    private async Task<Dictionary<string, string>> GetMetadata(Guid fileId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "SELECT * " +
            "FROM broker.file_metadata " +
            "WHERE file_id_fk = @fileId", connection))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            var metadata = new Dictionary<string, string>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    metadata.Add(reader.GetString(reader.GetOrdinal("key")), reader.GetString(reader.GetOrdinal("value")));
                }
            }
            return metadata;
        }
    }

    private async Task SetMetadata(Guid fileId, Dictionary<string, string> metadata)
    {
        var connection = await _connectionProvider.GetConnectionAsync();
        using var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand(
            "INSERT INTO broker.file_metadata (metadata_id_pk, file_id_fk, key, value) " +
            "VALUES (DEFAULT, @fileId, @key, @value)",
            connection);

        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.Add(new NpgsqlParameter("@key", NpgsqlDbType.Varchar));
        command.Parameters.Add(new NpgsqlParameter("@value", NpgsqlDbType.Varchar));

        try
        {
            foreach (var metadataEntry in metadata)
            {
                command.Parameters[1].Value = metadataEntry.Key;
                command.Parameters[2].Value = metadataEntry.Value;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new NpgsqlException("Failed while inserting metadata");
                }
            }
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
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
}

