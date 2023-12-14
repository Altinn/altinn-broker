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
        using var connection = await _connectionProvider.GetConnectionAsync();

        var file = new FileEntity();
        using var command = new NpgsqlCommand(
            @"
            SELECT file_id_pk, service_owner_id_fk, filename, checksum, sender_actor_id_fk, external_file_reference, created, file_location, expiration_time, 
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
            WHERE file_id_pk = @fileId",
            connection);
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                file = new FileEntity
                {
                    FileId = reader.GetGuid(reader.GetOrdinal("file_id_pk")),
                    ServiceOwnerId = reader.GetString(reader.GetOrdinal("service_owner_id_fk")),
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
        using var connection = await _connectionProvider.GetConnectionAsync();

        var fileStatuses = new List<ActorFileStatusEntity>();
        using (var command = new NpgsqlCommand(
            @"
            SELECT afs.actor_id_fk, MAX(afs.actor_file_status_id_fk) as actor_file_status_id_fk, MAX(afs.actor_file_status_date) as actor_file_status_date, a.actor_external_id 
            FROM broker.file 
            LEFT JOIN broker.actor_file_status afs on afs.file_id_fk = file_id_pk 
            LEFT JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk 
            WHERE file_id_pk = @fileId 
            GROUP BY afs.actor_id_fk, a.actor_external_id
        ", connection))
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

    public async Task<Guid> AddFile(ServiceOwnerEntity serviceOwner, string filename, string sendersFileReference, string senderExternalId, List<string> recipientIds, Dictionary<string, string> propertyList, string? checksum)
    {
        if (serviceOwner.StorageProvider is null)
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

        using var connection = await _connectionProvider.GetConnectionAsync();
        NpgsqlCommand command = new NpgsqlCommand(
            "INSERT INTO broker.file (file_id_pk, service_owner_id_fk, filename, checksum, external_file_reference, sender_actor_id_fk, created, storage_provider_id_fk, expiration_time) " +
            "VALUES (@fileId, @serviceOwnerId, @filename, @checksum, @externalFileReference, @senderActorId, @created, @storageProviderId, @expirationTime)",
            connection);

        var fileId = Guid.NewGuid();
        command.Parameters.AddWithValue("@fileId", fileId);
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwner.Id);
        command.Parameters.AddWithValue("@filename", filename);
        command.Parameters.AddWithValue("@checksum", checksum is null ? DBNull.Value : checksum);
        command.Parameters.AddWithValue("@senderActorId", actorId);
        command.Parameters.AddWithValue("@externalFileReference", sendersFileReference);
        command.Parameters.AddWithValue("@fileStatusId", (int)FileStatus.Initialized); // TODO, remove?
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@storageProviderId", serviceOwner.StorageProvider.Id);
        command.Parameters.AddWithValue("@expirationTime", DateTime.UtcNow.Add(serviceOwner.FileTimeToLive));

        command.ExecuteNonQuery();

        await SetMetadata(fileId, propertyList);

        return fileId;
    }

    public async Task<List<Guid>> GetFilesAssociatedWithActor(ActorEntity actor)
    {
        using var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "SELECT DISTINCT afs.file_id_fk, 'Recipient'  " +
            "FROM broker.actor_file_status afs " +
            "WHERE afs.actor_id_fk = @actorId " +
            "UNION " +
            "SELECT f.file_id_pk, 'Sender' " +
            "FROM broker.file f " +
            "INNER JOIN broker.actor a on a.actor_id_pk = f.sender_actor_id_fk " +
            "WHERE a.actor_external_id = @actorExternalId " +
            "UNION " +
            "SELECT f.file_id_pk, 'Service' " +
            "FROM broker.file f " +
            "WHERE f.service_owner_id_fk = @actorExternalId", connection))
        {
            command.Parameters.AddWithValue("@actorId", actor.ActorId);
            command.Parameters.AddWithValue("@actorExternalId", actor.ActorExternalId);

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
        using var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "UPDATE broker.file " +
            "SET " +
                "file_location = @fileLocation, " +
                "storage_provider_id_fk = @storageProviderId " +
            "WHERE file_id_pk = @fileId", connection))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
            command.Parameters.AddWithValue("@fileLocation", fileLocation);
            command.ExecuteNonQuery();
        }
    }

    private async Task<Dictionary<string, string>> GetMetadata(Guid fileId)
    {
        using var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
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
        using var connection = await _connectionProvider.GetConnectionAsync();
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
