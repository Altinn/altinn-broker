using System.Text;

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

    public async Task<FileEntity?> GetFile(Guid fileId)
    {
        var file = new FileEntity();

        using var command = await _connectionProvider.CreateCommand(
            @"
                SELECT file_id_pk, resource_id_fk, filename, checksum, sender_actor_id_fk, external_file_reference, created, file_location, expiration_time, 
                        sender.actor_external_id as senderActorExternalReference,
                    (
                        SELECT fs.file_status_description_id_fk 
                        FROM broker.file_status fs 
                        WHERE fs.file_id_fk = @fileId 
                        ORDER BY fs.file_status_date desc 
                        LIMIT 1
                    ) as file_status_description_id_fk, 
                    (
                        SELECT fs.file_status_date 
                        FROM broker.file_status fs 
                        WHERE fs.file_id_fk = @fileId 
                        ORDER BY fs.file_status_date desc 
                        LIMIT 1
                    ) as file_status_date 
                FROM broker.file 
                INNER JOIN broker.actor sender on sender.actor_id_pk = sender_actor_id_fk 
                WHERE file_id_pk = @fileId");
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                file = new FileEntity
                {
                    FileId = reader.GetGuid(reader.GetOrdinal("file_id_pk")),
                    ResourceId = reader.GetString(reader.GetOrdinal("resource_id_fk")),
                    Filename = reader.GetString(reader.GetOrdinal("filename")),
                    Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
                    SendersFileReference = reader.GetString(reader.GetOrdinal("external_file_reference")),
                    FileStatus = (FileStatus)reader.GetInt32(reader.GetOrdinal("file_status_description_id_fk")),
                    FileStatusChanged = reader.GetDateTime(reader.GetOrdinal("file_status_date")),
                    Created = reader.GetDateTime(reader.GetOrdinal("created")),
                    ExpirationTime = reader.GetDateTime(reader.GetOrdinal("expiration_time")),
                    FileLocation = reader.IsDBNull(reader.GetOrdinal("file_location")) ? null : reader.GetString(reader.GetOrdinal("file_location")),
                    Sender = new ActorEntity()
                    {
                        ActorId = reader.GetInt64(reader.GetOrdinal("sender_actor_id_fk")),
                        ActorExternalId = reader.GetString(reader.GetOrdinal("senderActorExternalReference"))
                    }
                };
            }
            else
            {
                return null;
            }
        }
        file.RecipientCurrentStatuses = await GetLatestRecipientFileStatuses(fileId);
        file.PropertyList = await GetMetadata(fileId);
        return file;
    }


    /*
     * Get the current status of file recipients along wiith the last time their status changed.  
     * */
    private async Task<List<ActorFileStatusEntity>> GetLatestRecipientFileStatuses(Guid fileId)
    {
        var fileStatuses = new List<ActorFileStatusEntity>();
        await using (var command = await _connectionProvider.CreateCommand(
            @"
            SELECT afs.actor_id_fk, MAX(afs.actor_file_status_id_fk) as actor_file_status_id_fk, MAX(afs.actor_file_status_date) as actor_file_status_date, a.actor_external_id 
            FROM broker.file 
            LEFT JOIN broker.actor_file_status afs on afs.file_id_fk = file_id_pk 
            LEFT JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk 
            WHERE file_id_pk = @fileId 
            GROUP BY afs.actor_id_fk, a.actor_external_id
        "))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            var commandText = command.CommandText;
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    fileStatuses.Add(new ActorFileStatusEntity()
                    {
                        FileId = fileId,
                        Status = (ActorFileStatus)reader.GetInt32(reader.GetOrdinal("actor_file_status_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("actor_file_status_date")),
                        Actor = new ActorEntity()
                        {
                            ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk")),
                            ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                        }
                    });
                }
            }
        }
        return fileStatuses;
    }

    public async Task<Guid> AddFile(ResourceOwnerEntity resourceOwner, ResourceEntity service, string filename, string sendersFileReference, string senderExternalId, List<string> recipientIds, Dictionary<string, string> propertyList, string? checksum)
    {
        if (resourceOwner.StorageProvider is null)
        {
            throw new ArgumentNullException("Storage provider must be set");
        }
        long actorId;
        var actor = await _actorRepository.GetActorAsync(senderExternalId);
        if (actor is null)
        {
            actorId = await _actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = senderExternalId
            });
        }
        else
        {
            actorId = actor.ActorId;
        }

        var fileId = Guid.NewGuid();
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.file (file_id_pk, resource_id_fk, filename, checksum, external_file_reference, sender_actor_id_fk, created, storage_provider_id_fk, expiration_time) " +
            "VALUES (@fileId, @serviceId, @filename, @checksum, @externalFileReference, @senderActorId, @created, @storageProviderId, @expirationTime)");

        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.AddWithValue("@serviceId", service.Id);
        command.Parameters.AddWithValue("@filename", filename);
        command.Parameters.AddWithValue("@checksum", checksum is null ? DBNull.Value : checksum);
        command.Parameters.AddWithValue("@senderActorId", actorId);
        command.Parameters.AddWithValue("@externalFileReference", sendersFileReference);
        command.Parameters.AddWithValue("@fileStatusId", (int)FileStatus.Initialized); // TODO, remove?
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@storageProviderId", resourceOwner.StorageProvider.Id);
        command.Parameters.AddWithValue("@expirationTime", DateTime.UtcNow.Add(resourceOwner.FileTimeToLive));

        command.ExecuteNonQuery();

        await SetMetadata(fileId, propertyList);
        return fileId;
    }

    public async Task<List<Guid>> GetFilesAssociatedWithActor(FileSearchEntity fileSearch)
    {
        StringBuilder commandString = new StringBuilder();
        commandString.AppendLine("SELECT DISTINCT afs.file_id_fk, 'Recipient'");
        commandString.AppendLine("FROM broker.actor_file_status afs ");
        commandString.AppendLine("INNER JOIN broker.file f on f.file_id_pk = afs.file_id_fk");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_status_description_id_fk FROM broker.file_status fs where fs.file_id_fk = f.file_id_pk ORDER BY fs.file_status_id_pk desc LIMIT 1 ) AS filestatus ON true");
        commandString.AppendLine("WHERE afs.actor_id_fk = @actorId");
        if (fileSearch.Status.HasValue)
        {
            commandString.AppendLine("AND filestatus.file_status_Description_id_fk = @fileStatus");
        }
        if (fileSearch.From.HasValue && fileSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }

        commandString.AppendLine("UNION");

        commandString.AppendLine("SELECT f.file_id_pk, 'Sender' ");
        commandString.AppendLine("FROM broker.file f ");
        commandString.AppendLine("INNER JOIN broker.actor a on a.actor_id_pk = f.sender_actor_id_fk ");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_status_description_id_fk FROM broker.file_status fs where fs.file_id_fk = f.file_id_pk ORDER BY fs.file_status_id_pk desc LIMIT 1 ) AS filestatus ON true");
        commandString.AppendLine("WHERE a.actor_external_id = @actorExternalId ");
        if (fileSearch.Status.HasValue)
        {
            commandString.AppendLine("AND filestatus.file_status_Description_id_fk = @fileStatus");
        }
        if (fileSearch.From.HasValue && fileSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }

        commandString.AppendLine(";");

        await using (var command = await _connectionProvider.CreateCommand(
            commandString.ToString()))
        {
            command.Parameters.AddWithValue("@actorId", fileSearch.Actor.ActorId);
            command.Parameters.AddWithValue("@actorExternalId", fileSearch.Actor.ActorExternalId);
            if (fileSearch.From.HasValue)
                command.Parameters.AddWithValue("@From", fileSearch.From);
            if (fileSearch.To.HasValue)
                command.Parameters.AddWithValue("@To", fileSearch.To);
            if (fileSearch.Status.HasValue)
                command.Parameters.AddWithValue("@fileStatus", (int)fileSearch.Status);

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

    public async Task<List<Guid>> GetFilesForRecipientWithRecipientStatus(FileSearchEntity fileSearch)
    {
        StringBuilder commandString = new StringBuilder();
        commandString.AppendLine("SELECT DISTINCT f.file_id_pk");
        commandString.AppendLine("FROM broker.file f");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT afs.actor_file_status_id_fk FROM broker.actor_file_status afs WHERE afs.file_id_fk = f.file_id_pk AND afs.actor_id_fk = @recipientId ORDER BY afs.actor_file_status_id_fk desc LIMIT 1) AS recipientfilestatus ON true");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_status_description_id_fk FROM broker.file_status fs where fs.file_id_fk = f.file_id_pk ORDER BY fs.file_status_id_pk desc LIMIT 1 ) AS filestatus ON true");
        commandString.AppendLine("WHERE actor_file_status_id_fk = @recipientFileStatus");
        if (fileSearch.Status.HasValue)
        {
            commandString.AppendLine("AND file_status_description_id_fk = @fileStatus");
        }
        if (fileSearch.From.HasValue && fileSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }

        await using (var command = await _connectionProvider.CreateCommand(
            commandString.ToString()))
        {
            command.Parameters.AddWithValue("@recipientId", fileSearch.Actor.ActorId);
            if (fileSearch.From.HasValue)
                command.Parameters.AddWithValue("@From", fileSearch.From);
            if (fileSearch.To.HasValue)
                command.Parameters.AddWithValue("@To", fileSearch.To);
            if (fileSearch.Status.HasValue)
                command.Parameters.AddWithValue("@fileStatus", (int)fileSearch.Status);
            if (fileSearch.RecipientStatus.HasValue)
                command.Parameters.AddWithValue("@recipientFileStatus", (int)fileSearch.RecipientStatus);

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

    public async Task SetStorageReference(Guid fileId, long storageProviderId, string fileLocation)
    {
        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.file " +
            "SET " +
                "file_location = @fileLocation, " +
                "storage_provider_id_fk = @storageProviderId " +
            "WHERE file_id_pk = @fileId"))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
            command.Parameters.AddWithValue("@fileLocation", fileLocation);
            command.ExecuteNonQuery();
        }
    }

    private async Task<Dictionary<string, string>> GetMetadata(Guid fileId)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = new NpgsqlCommand(
            "SELECT * " +
            "FROM broker.file_property " +
            "WHERE file_id_fk = @fileId", connection))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            var property = new Dictionary<string, string>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    property.Add(reader.GetString(reader.GetOrdinal("key")), reader.GetString(reader.GetOrdinal("value")));
                }
            }
            return property;
        }
    }

    private async Task SetMetadata(Guid fileId, Dictionary<string, string> property)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();
        using var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand(
            "INSERT INTO broker.file_property (property_id_pk, file_id_fk, key, value) " +
            "VALUES (DEFAULT, @fileId, @key, @value)",
            connection);

        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.Add(new NpgsqlParameter("@key", NpgsqlDbType.Varchar));
        command.Parameters.Add(new NpgsqlParameter("@value", NpgsqlDbType.Varchar));

        try
        {
            foreach (var propertyEntry in property)
            {
                command.Parameters[1].Value = propertyEntry.Key;
                command.Parameters[2].Value = propertyEntry.Value;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new NpgsqlException("Failed while inserting property");
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
}
