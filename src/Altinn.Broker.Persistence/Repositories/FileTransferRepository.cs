﻿using System.Text;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Broker.Persistence.Repositories;

public class FileTransferRepository : IFileTransferRepository
{
    private DatabaseConnectionProvider _connectionProvider;
    private readonly IActorRepository _actorRepository;

    public FileTransferRepository(DatabaseConnectionProvider connectionProvider, IActorRepository actorRepository)
    {
        _connectionProvider = connectionProvider;
        _actorRepository = actorRepository;
    }

    public async Task<FileTransferEntity?> GetFileTransfer(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransfer = new FileTransferEntity();

        await using var command = await _connectionProvider.CreateCommand(
            @"
                SELECT 
                    f.file_transfer_id_pk, 
                    f.resource_id, 
                    f.filename, 
                    f.checksum, 
                    f.sender_actor_id_fk, 
                    f.external_file_transfer_reference, 
                    f.created, 
                    f.file_location,
                    f.file_transfer_size,
                    f.expiration_time, 
                    f.hangfire_job_id,
                    sender.actor_external_id as senderActorExternalReference,
                    fs_latest.file_transfer_status_description_id_fk, 
                    fs_latest.file_transfer_status_date, 
                    fs_latest.file_transfer_status_detailed_description
                FROM 
                    broker.file_transfer f
                INNER JOIN 
                    broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
                LEFT JOIN 
                    (
                        SELECT 
                            fs.file_transfer_id_fk,
                            fs.file_transfer_status_description_id_fk,
                            fs.file_transfer_status_date,
                            fs.file_transfer_status_detailed_description
                        FROM 
                            broker.file_transfer_status fs
                        INNER JOIN 
                            (
                                SELECT 
                                    file_transfer_id_fk, 
                                    MAX(file_transfer_status_date) as max_date
                                FROM 
                                    broker.file_transfer_status 
                                GROUP BY 
                                    file_transfer_id_fk
                            ) fs_max ON fs.file_transfer_id_fk = fs_max.file_transfer_id_fk AND fs.file_transfer_status_date = fs_max.max_date
                        WHERE 
                            fs.file_transfer_id_fk = @fileTransferId
                    ) fs_latest ON f.file_transfer_id_pk = fs_latest.file_transfer_id_fk
                WHERE 
                    f.file_transfer_id_pk = @fileTransferId;");
        {
            command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                fileTransfer = new FileTransferEntity
                {
                    FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk")),
                    ResourceId = reader.GetString(reader.GetOrdinal("resource_id")),
                    FileName = reader.GetString(reader.GetOrdinal("filename")),
                    Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
                    SendersFileTransferReference = reader.GetString(reader.GetOrdinal("external_file_transfer_reference")),
                    HangfireJobId = reader.IsDBNull(reader.GetOrdinal("hangfire_job_id")) ? null : reader.GetString(reader.GetOrdinal("hangfire_job_id")),
                    FileTransferStatusEntity = new FileTransferStatusEntity()
                    {
                        FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk")),
                        Status = (FileTransferStatus)reader.GetInt32(reader.GetOrdinal("file_transfer_status_description_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("file_transfer_status_date")),
                        DetailedStatus = reader.IsDBNull(reader.GetOrdinal("file_transfer_status_detailed_description")) ? null : reader.GetString(reader.GetOrdinal("file_transfer_status_detailed_description"))
                    },
                    FileTransferStatusChanged = reader.GetDateTime(reader.GetOrdinal("file_transfer_status_date")),
                    Created = reader.GetDateTime(reader.GetOrdinal("created")),
                    ExpirationTime = reader.GetDateTime(reader.GetOrdinal("expiration_time")),
                    FileLocation = reader.IsDBNull(reader.GetOrdinal("file_location")) ? null : reader.GetString(reader.GetOrdinal("file_location")),
                    FileTransferSize = reader.IsDBNull(reader.GetOrdinal("file_transfer_size")) ? 0 : reader.GetInt64(reader.GetOrdinal("file_transfer_size")),
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
        fileTransfer.RecipientCurrentStatuses = await GetLatestRecipientFileTransferStatuses(fileTransferId, cancellationToken);
        fileTransfer.PropertyList = await GetMetadata(fileTransferId, cancellationToken);
        return fileTransfer;
    }


    /*
     * Get the current status of a file tranfer's recipients along wiith the last time their status changed.  
     * */
    private async Task<List<ActorFileTransferStatusEntity>> GetLatestRecipientFileTransferStatuses(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransferStatuses = new List<ActorFileTransferStatusEntity>();
        await using (var command = await _connectionProvider.CreateCommand(
            @"
            SELECT afs.actor_id_fk, MAX(afs.actor_file_transfer_status_description_id_fk) as actor_file_transfer_status_description_id_fk, MAX(afs.actor_file_transfer_status_date) as actor_file_transfer_status_date, a.actor_external_id 
            FROM broker.file_transfer 
            LEFT JOIN broker.actor_file_transfer_status afs on afs.file_transfer_id_fk = file_transfer_id_pk 
            LEFT JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk 
            WHERE file_transfer_id_pk = @fileTransferId 
            GROUP BY afs.actor_id_fk, a.actor_external_id
        "))
        {
            command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
            var commandText = command.CommandText;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    fileTransferStatuses.Add(new ActorFileTransferStatusEntity()
                    {
                        FileTransferId = fileTransferId,
                        Status = (ActorFileTransferStatus)reader.GetInt32(reader.GetOrdinal("actor_file_transfer_status_description_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("actor_file_transfer_status_date")),
                        Actor = new ActorEntity()
                        {
                            ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk")),
                            ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                        }
                    });
                }
            }
        }
        return fileTransferStatuses;
    }

    public async Task<Guid> AddFileTransfer(ServiceOwnerEntity serviceOwner, ResourceEntity resource, string fileName, string sendersFileTransferReference, string senderExternalId, List<string> recipientIds, Dictionary<string, string> propertyList, string? checksum, long? fileTransferSize, string? hangfireJobId, CancellationToken cancellationToken = default)
    {

        if (serviceOwner.StorageProvider is null)
        {
            throw new ArgumentNullException("Storage provider must be set");
        }
        long actorId;
        var actor = await _actorRepository.GetActorAsync(senderExternalId, cancellationToken);
        if (actor is null)
        {
            actorId = await _actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = senderExternalId
            }, cancellationToken);
        }
        else
        {
            actorId = actor.ActorId;
        }
        var fileTransferId = Guid.NewGuid();
        await using NpgsqlCommand command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.file_transfer (file_transfer_id_pk, resource_id, filename, checksum, file_transfer_size, external_file_transfer_reference, sender_actor_id_fk, created, storage_provider_id_fk, expiration_time, hangfire_job_id) " +
            "VALUES (@fileTransferId, @resourceId, @fileName, @checksum, @fileTransferSize, @externalFileTransferReference, @senderActorId, @created, @storageProviderId, @expirationTime, @hangfireJobId)");


        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@resourceId", resource.Id);
        command.Parameters.AddWithValue("@fileName", fileName);
        command.Parameters.AddWithValue("@checksum", checksum is null ? DBNull.Value : checksum);
        command.Parameters.AddWithValue("@fileTransferSize", fileTransferSize is null ? DBNull.Value : fileTransferSize);
        command.Parameters.AddWithValue("@senderActorId", actorId);
        command.Parameters.AddWithValue("@externalFileTransferReference", sendersFileTransferReference);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@storageProviderId", serviceOwner.StorageProvider.Id);
        command.Parameters.AddWithValue("@hangfireJobId", hangfireJobId is null ? DBNull.Value : hangfireJobId);
        command.Parameters.AddWithValue("@expirationTime", DateTime.UtcNow.Add(serviceOwner.FileTransferTimeToLive));

        await command.ExecuteNonQueryAsync(cancellationToken);

        await SetMetadata(fileTransferId, propertyList, cancellationToken);
        return fileTransferId;
    }

    public async Task<List<Guid>> LegacyGetFilesForRecipientsWithRecipientStatus(LegacyFileSearchEntity fileTransferSearch, CancellationToken cancellationToken)
    {
        StringBuilder commandString = new StringBuilder();
        commandString.AppendLine("SELECT DISTINCT f.file_transfer_id_pk");
        commandString.AppendLine("FROM broker.file_transfer f");
        commandString.AppendLine("INNER JOIN LATERAL ");
        commandString.AppendLine("(SELECT afs.actor_file_transfer_status_description_id_fk FROM broker.actor_file_transfer_status afs ");
        commandString.AppendLine("WHERE afs.file_transfer_id_fk = f.file_transfer_id_pk ");
        if (fileTransferSearch.Actors?.Count > 0)
        {
            commandString.AppendLine($"AND afs.actor_id_fk in ({string.Join(',', fileTransferSearch.Actors.Select(a => a.ActorId))})");
        }
        else
        {
            commandString.AppendLine("AND afs.actor_id_fk = @actorId");
        }
        commandString.AppendLine("ORDER BY afs.actor_file_transfer_status_description_id_fk desc LIMIT 1) AS recipientfiletransferstatus ON true");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true");
        commandString.AppendLine("WHERE 1 = 1");
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileTransferSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }
        if (!string.IsNullOrWhiteSpace(fileTransferSearch.ResourceId))
        {
            commandString.AppendLine("AND resource_id = @resourceId");
        }
        if (fileTransferSearch.RecipientFileTransferStatus.HasValue)
        {
            if(fileTransferSearch.RecipientFileTransferStatus.Value == ActorFileTransferStatus.Initialized)
            {
                commandString.AppendLine($"AND actor_file_transfer_status_description_id_fk in (0,1)");
            }
            else
            {
                commandString.AppendLine("AND actor_file_transfer_status_description_id_fk = @recipientFileTransferStatus");
            }
        }
        if (fileTransferSearch.FileTransferStatus.HasValue)
        {
            commandString.AppendLine("AND filetransferstatus.file_transfer_status_description_id_fk = @fileTransferStatus");
        }

        commandString.AppendLine(";");

        await using (var command = await _connectionProvider.CreateCommand(
            commandString.ToString()))
        {
            if (!(fileTransferSearch.Actor is null))
            {
                command.Parameters.AddWithValue("@actorId", fileTransferSearch.Actor.ActorId);
            }

            if (!string.IsNullOrWhiteSpace(fileTransferSearch.ResourceId))
            {
                command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
            }

            if (fileTransferSearch.From.HasValue)
                command.Parameters.AddWithValue("@From", fileTransferSearch.From);
            if (fileTransferSearch.To.HasValue)
                command.Parameters.AddWithValue("@To", fileTransferSearch.To);
            if (fileTransferSearch.RecipientFileTransferStatus.HasValue && fileTransferSearch.RecipientFileTransferStatus.Value != ActorFileTransferStatus.Initialized)
                command.Parameters.AddWithValue("@recipientFileTransferStatus", (int)fileTransferSearch.RecipientFileTransferStatus);
            if (fileTransferSearch.FileTransferStatus.HasValue)
                command.Parameters.AddWithValue("@fileTransferStatus", (int)fileTransferSearch.FileTransferStatus);

            var fileTransfers = new List<Guid>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var fileTransferId = reader.GetGuid(0);
                    fileTransfers.Add(fileTransferId);
                }
            }
            return fileTransfers;
        }
    }

    public async Task<List<Guid>> GetFileTransfersAssociatedWithActor(FileTransferSearchEntity fileTransferSearch, CancellationToken cancellationToken)
    {
        StringBuilder commandString = new StringBuilder();
        commandString.AppendLine("SELECT DISTINCT afs.file_transfer_id_fk, 'Recipient'");
        commandString.AppendLine("FROM broker.actor_file_transfer_status afs ");
        commandString.AppendLine("INNER JOIN broker.file_transfer f on f.file_transfer_id_pk = afs.file_transfer_id_fk");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true");
        commandString.AppendLine("WHERE afs.actor_id_fk = @actorId AND f.resource_id = @resourceId");
        if (fileTransferSearch.Status.HasValue)
        {
            commandString.AppendLine("AND filetransferstatus.file_transfer_status_Description_id_fk = @fileTransferStatus");
        }
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileTransferSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }

        commandString.AppendLine("UNION");

        commandString.AppendLine("SELECT f.file_transfer_id_pk, 'Sender' ");
        commandString.AppendLine("FROM broker.file_transfer f ");
        commandString.AppendLine("INNER JOIN broker.actor a on a.actor_id_pk = f.sender_actor_id_fk ");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true");
        commandString.AppendLine("WHERE a.actor_external_id = @actorExternalId AND resource_id = @resourceId");
        if (fileTransferSearch.Status.HasValue)
        {
            commandString.AppendLine("AND filetransferstatus.file_transfer_status_Description_id_fk = @fileTransferStatus");
        }
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileTransferSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }

        commandString.AppendLine(";");

        await using (var command = await _connectionProvider.CreateCommand(
            commandString.ToString()))
        {
            command.Parameters.AddWithValue("@actorId", fileTransferSearch.Actor.ActorId);
            command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
            command.Parameters.AddWithValue("@actorExternalId", fileTransferSearch.Actor.ActorExternalId);
            if (fileTransferSearch.From.HasValue)
                command.Parameters.AddWithValue("@From", fileTransferSearch.From);
            if (fileTransferSearch.To.HasValue)
                command.Parameters.AddWithValue("@To", fileTransferSearch.To);
            if (fileTransferSearch.Status.HasValue)
                command.Parameters.AddWithValue("@fileTransferStatus", (int)fileTransferSearch.Status);

            var files = new List<Guid>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var fileTransferId = reader.GetGuid(0);
                    files.Add(fileTransferId);
                }
            }
            return files;
        }
    }

    public async Task<List<Guid>> GetFileTransfersForRecipientWithRecipientStatus(FileTransferSearchEntity fileTransferSearch, CancellationToken cancellationToken)
    {
        StringBuilder commandString = new StringBuilder();
        commandString.AppendLine("SELECT DISTINCT f.file_transfer_id_pk");
        commandString.AppendLine("FROM broker.file_transfer f");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT afs.actor_file_transfer_status_description_id_fk FROM broker.actor_file_transfer_status afs WHERE afs.file_transfer_id_fk = f.file_transfer_id_pk AND afs.actor_id_fk = @recipientId ORDER BY afs.actor_file_transfer_status_description_id_fk desc LIMIT 1) AS recipientfilestatus ON true");
        commandString.AppendLine("INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filestatus ON true");
        commandString.AppendLine("WHERE actor_file_transfer_status_description_id_fk = @recipientFileStatus AND resource_id = @resourceId");
        if (fileTransferSearch.Status.HasValue)
        {
            commandString.AppendLine("AND file_transfer_status_description_id_fk = @fileStatus");
        }
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created between @from AND @to");
        }
        else if (fileTransferSearch.From.HasValue)
        {
            commandString.AppendLine("AND f.created > @from");
        }
        else if (fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("AND f.created < @to");
        }

        await using (var command = await _connectionProvider.CreateCommand(
            commandString.ToString()))
        {
            command.Parameters.AddWithValue("@recipientId", fileTransferSearch.Actor.ActorId);
            command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
            if (fileTransferSearch.From.HasValue)
                command.Parameters.AddWithValue("@From", fileTransferSearch.From);
            if (fileTransferSearch.To.HasValue)
                command.Parameters.AddWithValue("@To", fileTransferSearch.To);
            if (fileTransferSearch.Status.HasValue)
                command.Parameters.AddWithValue("@fileStatus", (int)fileTransferSearch.Status);
            if (fileTransferSearch.RecipientStatus.HasValue)
                command.Parameters.AddWithValue("@recipientFileStatus", (int)fileTransferSearch.RecipientStatus);

            var files = new List<Guid>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var fileTransferId = reader.GetGuid(0);
                    files.Add(fileTransferId);
                }
            }
            return files;
        }
    }

    public async Task SetStorageDetails(Guid fileTransferId, long storageProviderId, string fileLocation, long filesize, CancellationToken cancellationToken)
    {
        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.file_transfer " +
            "SET " +
                "file_location = @fileLocation, " +
                "file_transfer_size = @filesize, " +
                "storage_provider_id_fk = @storageProviderId " +
            "WHERE file_transfer_id_pk = @fileTransferId"))
        {
            command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
            command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
            command.Parameters.AddWithValue("@fileLocation", fileLocation);
            command.Parameters.AddWithValue("@filesize", filesize);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, string>> GetMetadata(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = new NpgsqlCommand(
            "SELECT * " +
            "FROM broker.file_transfer_property " +
            "WHERE file_transfer_id_fk = @fileTransferId", connection))
        {
            command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
            var property = new Dictionary<string, string>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    property.Add(reader.GetString(reader.GetOrdinal("key")), reader.GetString(reader.GetOrdinal("value")));
                }
            }
            return property;
        }
    }

    private async Task SetMetadata(Guid fileTransferId, Dictionary<string, string> property, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();
        using var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand(
            "INSERT INTO broker.file_transfer_property (property_id_pk, file_transfer_id_fk, key, value) " +
            "VALUES (DEFAULT, @fileTransferId, @key, @value)",
            connection);

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
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

    public async Task SetChecksum(Guid fileTransferId, string checksum, CancellationToken cancellationToken)
    {
        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.file_transfer " +
            "SET " +
                "checksum = @checksum " +
            "WHERE file_transfer_id_pk = @fileTransferId"))
        {
            command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
            command.Parameters.AddWithValue("@checksum", checksum);
            command.ExecuteNonQuery();
        }
    }
    public async Task SetFileTransferHangfireJobId(Guid fileTransferId, string hangfireJobId, CancellationToken cancellationToken)
    {
        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.file_transfer " +
            "SET " +
                "hangfire_job_id = @hangfireJobId " +
            "WHERE file_transfer_id_pk = @fileTransferId"))
        {
            command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
            command.Parameters.AddWithValue("@hangfireJobId", hangfireJobId is null ? DBNull.Value : hangfireJobId);
            command.ExecuteNonQuery();
        }
    }
    public async Task<List<FileTransferEntity>> GetNonDeletedFileTransfersByStorageProvider(long storageProviderId, CancellationToken cancellationToken)
    {
        var fileTransfers = new List<FileTransferEntity>();
        await using (var command = await _connectionProvider.CreateCommand(
                @"SELECT
                f.*,
                (
                    SELECT 
                        fs.file_transfer_status_description_id_fk
                    FROM 
                        broker.file_transfer_status fs
                    WHERE 
                        fs.file_transfer_id_fk = f.file_transfer_id_pk
                    ORDER BY 
                        fs.file_transfer_status_date DESC
                    LIMIT 1
                ) AS file_transfer_status_description_id_fk
            FROM 
                broker.file_transfer f
            WHERE 
                f.storage_provider_id_fk = @storageProviderId
                AND (
                    SELECT 
                        fs.file_transfer_status_id_pk
                    FROM 
                        broker.file_transfer_status fs
                    WHERE 
                        fs.file_transfer_id_fk = f.file_transfer_id_pk
                    ORDER BY 
                        fs.file_transfer_status_date DESC
                    LIMIT 1
                ) != @deletedStatusId;"))
        {
            command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
            command.Parameters.AddWithValue("@deletedStatusId", (int)FileTransferStatus.Deleted);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    fileTransfers.Add(new FileTransferEntity()
                    {
                        FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk")),
                        ResourceId = reader.GetString(reader.GetOrdinal("resource_id")),
                        FileName = reader.GetString(reader.GetOrdinal("filename")),
                        Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
                        SendersFileTransferReference = reader.GetString(reader.GetOrdinal("external_file_transfer_reference")),
                        HangfireJobId = reader.IsDBNull(reader.GetOrdinal("hangfire_job_id")) ? null : reader.GetString(reader.GetOrdinal("hangfire_job_id")),
                        Created = reader.GetDateTime(reader.GetOrdinal("created")),
                        ExpirationTime = reader.GetDateTime(reader.GetOrdinal("expiration_time")),
                        FileLocation = reader.IsDBNull(reader.GetOrdinal("file_location")) ? null : reader.GetString(reader.GetOrdinal("file_location")),
                        FileTransferSize = reader.IsDBNull(reader.GetOrdinal("file_transfer_size")) ? 0 : reader.GetInt64(reader.GetOrdinal("file_transfer_size")),
                    });
                }
            }
        }
        return fileTransfers;
    }

}

