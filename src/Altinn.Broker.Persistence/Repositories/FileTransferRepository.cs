using System.Text;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

using NpgsqlTypes;

using Serilog.Context;

namespace Altinn.Broker.Persistence.Repositories;

public class FileTransferRepository(NpgsqlDataSource dataSource, IActorRepository actorRepository) : IFileTransferRepository
{
    public async Task<FileTransferEntity?> GetFileTransfer(Guid fileTransferId, CancellationToken cancellationToken)
    {
        FileTransferEntity fileTransfer;

        await using var command = dataSource.CreateCommand(
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
                    f.use_virus_scan,
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
                    },
                    RecipientCurrentStatuses = await GetLatestRecipientFileTransferStatuses(fileTransferId, cancellationToken),
                    PropertyList = await GetMetadata(fileTransferId, cancellationToken),
                    UseVirusScan = reader.GetBoolean(reader.GetOrdinal("use_virus_scan"))
                };
            }
            else
            {
                return null;
            }
        }
        EnrichLogs(fileTransfer);
        return fileTransfer;
    }

    private static void EnrichLogs(FileTransferEntity fileTransferEntity)
    {
        LogContext.PushProperty("fileTransferId", fileTransferEntity.FileTransferId);
        LogContext.PushProperty("resourceId", fileTransferEntity.ResourceId);
        LogContext.PushProperty("sender", fileTransferEntity.Sender);
        LogContext.PushProperty("recipients", string.Join(',', fileTransferEntity.RecipientCurrentStatuses.Select(status => status.Actor.ActorExternalId)));
        LogContext.PushProperty("fileName", fileTransferEntity.FileName);
        LogContext.PushProperty("status", fileTransferEntity.FileTransferStatusEntity.Status.ToString());
    }

    /*
     * Get the current status of a file tranfer's recipients along wiith the last time their status changed.  
     * */
    private async Task<List<ActorFileTransferStatusEntity>> GetLatestRecipientFileTransferStatuses(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransferStatuses = new List<ActorFileTransferStatusEntity>();
        await using (var command = dataSource.CreateCommand(
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
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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
        return fileTransferStatuses;
    }

    public async Task<Guid> AddFileTransfer(ResourceEntity resource, StorageProviderEntity storageProviderEntity, string fileName, string sendersFileTransferReference, string senderExternalId, List<string> recipientIds, DateTimeOffset expirationTime, Dictionary<string, string> propertyList, string? checksum, bool useVirusScan, CancellationToken cancellationToken = default)
    {
        long actorId;
        var actor = await actorRepository.GetActorAsync(senderExternalId, cancellationToken);
        if (actor is null)
        {
            actorId = await actorRepository.AddActorAsync(new ActorEntity()
            {
                ActorExternalId = senderExternalId
            }, cancellationToken);
        }
        else
        {
            actorId = actor.ActorId;
        }
        var fileTransferId = Guid.NewGuid();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO broker.file_transfer (file_transfer_id_pk, resource_id, filename, checksum, file_transfer_size, external_file_transfer_reference, sender_actor_id_fk, created, storage_provider_id_fk, expiration_time, hangfire_job_id, use_virus_scan) " +
            "VALUES (@fileTransferId, @resourceId, @fileName, @checksum, @fileTransferSize, @externalFileTransferReference, @senderActorId, @created, @storageProviderId, @expirationTime, @hangfireJobId, @useVirusScan)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@resourceId", resource.Id);
        command.Parameters.AddWithValue("@fileName", fileName);
        command.Parameters.AddWithValue("@checksum", checksum is null ? DBNull.Value : checksum);
        command.Parameters.AddWithValue("@fileTransferSize", DBNull.Value);
        command.Parameters.AddWithValue("@senderActorId", actorId);
        command.Parameters.AddWithValue("@externalFileTransferReference", sendersFileTransferReference);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@storageProviderId", storageProviderEntity.Id);
        command.Parameters.AddWithValue("@hangfireJobId", DBNull.Value);
        command.Parameters.AddWithValue("@expirationTime", expirationTime);
        command.Parameters.AddWithValue("@useVirusScan", useVirusScan);

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
        else if (!(fileTransferSearch.Actor is null))
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
            if (fileTransferSearch.RecipientFileTransferStatus.Value == ActorFileTransferStatus.Initialized)
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

        await using var command = dataSource.CreateCommand(
            commandString.ToString());
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

    public async Task<List<Guid>> GetFileTransfersAssociatedWithActor(FileTransferSearchEntity fileTransferSearch, CancellationToken cancellationToken)
    {
        string commandString = @"
            SELECT DISTINCT afs.file_transfer_id_fk, 'Recipient', f.created
            FROM broker.actor_file_transfer_status afs 
            INNER JOIN broker.file_transfer f on f.file_transfer_id_pk = afs.file_transfer_id_fk
            INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true
            WHERE afs.actor_id_fk = @actorId AND f.resource_id = @resourceId
            {0}
            {1}

            UNION

            SELECT f.file_transfer_id_pk, 'Sender', f.created 
            FROM broker.file_transfer f 
            INNER JOIN broker.actor a on a.actor_id_pk = f.sender_actor_id_fk 
            INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true
            WHERE a.actor_external_id = @actorExternalId AND resource_id = @resourceId
            {0}
            {1}

            ORDER BY created DESC
            LIMIT 100;";

        string statusCondition = fileTransferSearch.Status.HasValue
            ? "AND filetransferstatus.file_transfer_status_Description_id_fk = @fileTransferStatus"
            : "";

        string dateCondition = "";
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            dateCondition = "AND f.created between @from AND @to";
        }
        else if (fileTransferSearch.From.HasValue)
        {
            dateCondition = "AND f.created > @from";
        }
        else if (fileTransferSearch.To.HasValue)
        {
            dateCondition = "AND f.created < @to";
        }

        commandString = string.Format(commandString, statusCondition, dateCondition);

        await using (var command = dataSource.CreateCommand(commandString))
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
        string commandString = @"
            SELECT DISTINCT f.file_transfer_id_pk, f.created
            FROM broker.file_transfer f
            INNER JOIN LATERAL (
                SELECT afs.actor_file_transfer_status_description_id_fk 
                FROM broker.actor_file_transfer_status afs 
                WHERE afs.file_transfer_id_fk = f.file_transfer_id_pk AND afs.actor_id_fk = @recipientId 
                ORDER BY afs.actor_file_transfer_status_description_id_fk desc 
                LIMIT 1
            ) AS recipientfilestatus ON true
            INNER JOIN LATERAL (
                SELECT fs.file_transfer_status_description_id_fk 
                FROM broker.file_transfer_status fs 
                WHERE fs.file_transfer_id_fk = f.file_transfer_id_pk 
                ORDER BY fs.file_transfer_status_id_pk desc 
                LIMIT 1 
            ) AS filestatus ON true
            WHERE actor_file_transfer_status_description_id_fk = @recipientFileStatus AND resource_id = @resourceId
            {0}
            {1}
            ORDER BY created DESC
            LIMIT 100;";

        string statusCondition = fileTransferSearch.Status.HasValue
            ? "AND file_transfer_status_description_id_fk = @fileStatus"
            : "";

        string dateCondition = "";
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            dateCondition = "AND f.created BETWEEN @from AND @to";
        }
        else if (fileTransferSearch.From.HasValue)
        {
            dateCondition = "AND f.created > @from";
        }
        else if (fileTransferSearch.To.HasValue)
        {
            dateCondition = "AND f.created < @to";
        }

        commandString = string.Format(commandString, statusCondition, dateCondition);

        await using (var command = dataSource.CreateCommand(commandString))
        {
            command.Parameters.AddWithValue("@recipientId", fileTransferSearch.Actor.ActorId);
            command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
            command.Parameters.AddWithValue("@recipientFileStatus", (int)fileTransferSearch.RecipientStatus);

            if (fileTransferSearch.From.HasValue)
                command.Parameters.AddWithValue("@from", fileTransferSearch.From);
            if (fileTransferSearch.To.HasValue)
                command.Parameters.AddWithValue("@to", fileTransferSearch.To);
            if (fileTransferSearch.Status.HasValue)
                command.Parameters.AddWithValue("@fileStatus", (int)fileTransferSearch.Status);

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
        await using (var command = dataSource.CreateCommand(
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
        await using var command = dataSource.CreateCommand(
            "SELECT * " +
            "FROM broker.file_transfer_property " +
            "WHERE file_transfer_id_fk = @fileTransferId");
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

    private async Task SetMetadata(Guid fileTransferId, Dictionary<string, string> property, CancellationToken cancellationToken)
    {
        if (property.Count == 0)
        {
            return;
        }

        var valuesList = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var index = 0;

        foreach (var propertyEntry in property)
        {
            valuesList.Add($"(@fileTransferId, @key{index}, @value{index})");
            parameters.Add(new NpgsqlParameter($"@key{index}", NpgsqlDbType.Varchar) { Value = propertyEntry.Key });
            parameters.Add(new NpgsqlParameter($"@value{index}", NpgsqlDbType.Varchar) { Value = propertyEntry.Value });
            index++;
        }

        var valuesString = string.Join(", ", valuesList);
        var query = $@"
        INSERT INTO broker.file_transfer_property (file_transfer_id_fk, key, value)
        VALUES {valuesString}";

        using var command = dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddRange(parameters.ToArray());

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected != property.Count)
        {
            throw new NpgsqlException("Failed while inserting properties");
        }
    }

    public async Task SetChecksum(Guid fileTransferId, string checksum, CancellationToken cancellationToken)
    {
        await using (var command = dataSource.CreateCommand(
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
        await using (var command = dataSource.CreateCommand(
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
}

