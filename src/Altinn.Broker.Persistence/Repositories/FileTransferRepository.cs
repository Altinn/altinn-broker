using System.Text;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

using NpgsqlTypes;

using Serilog.Context;

namespace Altinn.Broker.Persistence.Repositories;

public class FileTransferRepository(NpgsqlDataSource dataSource, IActorRepository actorRepository, ExecuteDBCommandWithRetries commandExecutor) : IFileTransferRepository
{
    #region constants
    const string overviewCommandMultiple = @"
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
                            fs.file_transfer_id_fk IN (select unnest(@fileTransferIds))
                    ) fs_latest ON f.file_transfer_id_pk = fs_latest.file_transfer_id_fk
                WHERE 
                    f.file_transfer_id_pk IN (select unnest(@fileTransferIds));";
    const string overviewCommandSingle = @"
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
                    f.file_transfer_id_pk = @fileTransferId;";
    #endregion
    public async Task<IReadOnlyList<FileTransferEntity>> GetFileTransfers(IReadOnlyCollection<Guid> fileTransferIds, CancellationToken cancellationToken)
    {
        if (fileTransferIds is null || fileTransferIds.Count == 0)
        {
            return new List<FileTransferEntity>(0);
        }

        await using var command = dataSource.CreateCommand(overviewCommandMultiple);

        var parameter = new NpgsqlParameter("@fileTransferIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = fileTransferIds.ToArray()
        };

        command.Parameters.Add(parameter);
        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            List<FileTransferEntity> fileTransferEntities = new List<FileTransferEntity>();
            FileTransferEntity? fileTransfer = null;

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk"));
                fileTransfer = new FileTransferEntity
                {
                    FileTransferId = fileTransferId,
                    ResourceId = reader.GetString(reader.GetOrdinal("resource_id")),
                    FileName = reader.GetString(reader.GetOrdinal("filename")),
                    Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
                    SendersFileTransferReference = reader.GetString(reader.GetOrdinal("external_file_transfer_reference")),
                    HangfireJobId = reader.IsDBNull(reader.GetOrdinal("hangfire_job_id")) ? null : reader.GetString(reader.GetOrdinal("hangfire_job_id")),
                    FileTransferStatusEntity = new FileTransferStatusEntity()
                    {
                        FileTransferId = fileTransferId,
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
                    RecipientCurrentStatuses = await GetLatestRecipientFileTransferStatuses(fileTransferId, ct),
                    PropertyList = await GetMetadata(fileTransferId, ct),
                    UseVirusScan = reader.GetBoolean(reader.GetOrdinal("use_virus_scan"))
                };

                fileTransferEntities.Add(fileTransfer);
                EnrichLogs(fileTransfer);
            }

            return fileTransferEntities;
        }, cancellationToken);
    }

    public async Task<FileTransferEntity?> GetFileTransfer(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(overviewCommandSingle);

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            FileTransferEntity? fileTransfer = null;

            await using var reader = await command.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
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
                    RecipientCurrentStatuses = await GetLatestRecipientFileTransferStatuses(fileTransferId, ct),
                    PropertyList = await GetMetadata(fileTransferId, ct),
                    UseVirusScan = reader.GetBoolean(reader.GetOrdinal("use_virus_scan"))
                };

                EnrichLogs(fileTransfer);
            }

            return fileTransfer;
        }, cancellationToken);
    }

    private static void EnrichLogs(FileTransferEntity fileTransferEntity)
    {
        LogContext.PushProperty("instanceId", fileTransferEntity.FileTransferId);
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
        await using var command = dataSource.CreateCommand(
            @"
            SELECT afs.actor_id_fk, MAX(afs.actor_file_transfer_status_description_id_fk) as actor_file_transfer_status_description_id_fk, MAX(afs.actor_file_transfer_status_date) as actor_file_transfer_status_date, a.actor_external_id 
            FROM broker.file_transfer 
            LEFT JOIN broker.actor_file_transfer_status afs on afs.file_transfer_id_fk = file_transfer_id_pk 
            LEFT JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk 
            WHERE file_transfer_id_pk = @fileTransferId 
            GROUP BY afs.actor_id_fk, a.actor_external_id
        ");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransferStatuses = new List<ActorFileTransferStatusEntity>();
            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
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

            return fileTransferStatuses;
        }, cancellationToken);
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

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);

        await SetMetadata(fileTransferId, propertyList, cancellationToken);
        return fileTransferId;
    }
    public async Task<List<Guid>> LegacyGetFilesForRecipientsWithRecipientStatus(
    LegacyFileSearchEntity fileTransferSearch,
    CancellationToken cancellationToken)
    {
        StringBuilder commandString = new StringBuilder();
        long[] actorIds = fileTransferSearch.GetActorIds();

        commandString.AppendLine(@"
SELECT f.file_transfer_id_pk, f.created
FROM broker.file_transfer f");

        // Join the denormalized actor status table
        if (actorIds.Length > 0 || fileTransferSearch.RecipientFileTransferStatus.HasValue)
        {
            commandString.AppendLine(@"
INNER JOIN broker.actor_file_transfer_latest_status afls
  ON afls.file_transfer_id_fk = f.file_transfer_id_pk");
        }

        commandString.AppendLine("WHERE 1=1");

        // Actor filtering
        if (actorIds.Length > 1)
        {
            commandString.AppendLine("  AND afls.actor_id_fk = ANY(@actorIds)");
        }
        else if (actorIds.Length == 1)
        {
            commandString.AppendLine("  AND afls.actor_id_fk = @actorId");
        }

        // File transfer status filtering (using denormalized column)
        if (fileTransferSearch.FileTransferStatus.HasValue)
        {
            commandString.AppendLine("  AND f.latest_file_status_id = @fileTransferStatus");
        }

        // Recipient status filtering (using denormalized table)
        if (fileTransferSearch.RecipientFileTransferStatus.HasValue)
        {
            if (fileTransferSearch.RecipientFileTransferStatus.Value == ActorFileTransferStatus.Initialized)
            {
                commandString.AppendLine("  AND afls.latest_actor_status_id IN (0,1)");
            }
            else
            {
                commandString.AppendLine("  AND afls.latest_actor_status_id = @recipientFileTransferStatus");
            }
        }

        // Date range filtering
        if (fileTransferSearch.From.HasValue && fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("  AND f.created BETWEEN @from AND @to");
        }
        else if (fileTransferSearch.From.HasValue)
        {
            commandString.AppendLine("  AND f.created > @from");
        }
        else if (fileTransferSearch.To.HasValue)
        {
            commandString.AppendLine("  AND f.created < @to");
        }

        // Resource ID filtering
        if (!string.IsNullOrWhiteSpace(fileTransferSearch.ResourceId))
        {
            commandString.AppendLine("  AND f.resource_id = @resourceId");
        }

        commandString.AppendLine("ORDER BY f.created ASC;");

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            await using var command = dataSource.CreateCommand(commandString.ToString());

            if (actorIds.Length > 1)
            {
                command.Parameters.Add(new NpgsqlParameter("@actorIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
                {
                    Value = actorIds
                });
            }
            else if (actorIds.Length == 1)
            {
                command.Parameters.AddWithValue("@actorId", actorIds[0]);
            }

            if (!string.IsNullOrWhiteSpace(fileTransferSearch.ResourceId))
            {
                command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
            }

            if (fileTransferSearch.From.HasValue)
                command.Parameters.AddWithValue("@from", fileTransferSearch.From);
            if (fileTransferSearch.To.HasValue)
                command.Parameters.AddWithValue("@to", fileTransferSearch.To);
            if (fileTransferSearch.RecipientFileTransferStatus.HasValue &&
                fileTransferSearch.RecipientFileTransferStatus.Value != ActorFileTransferStatus.Initialized)
                command.Parameters.AddWithValue("@recipientFileTransferStatus",
                    (int)fileTransferSearch.RecipientFileTransferStatus);
            if (fileTransferSearch.FileTransferStatus.HasValue)
                command.Parameters.AddWithValue("@fileTransferStatus",
                    (int)fileTransferSearch.FileTransferStatus);

            var fileTransfers = new List<Guid>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(0);
                fileTransfers.Add(fileTransferId);
            }
            return fileTransfers;
        }, cancellationToken);
    }

    public async Task<List<Guid>> GetFileTransfersAssociatedWithActor(FileTransferSearchEntity fileTransferSearch, CancellationToken cancellationToken)
    {
        string recipientSelect = @"
            SELECT DISTINCT afs.file_transfer_id_fk as file_transfer_id, f.created
            FROM broker.actor_file_transfer_status afs 
            INNER JOIN broker.file_transfer f on f.file_transfer_id_pk = afs.file_transfer_id_fk
            INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true
            WHERE afs.actor_id_fk = @actorId AND f.resource_id = @resourceId
            {0}
            {1}";

        string senderSelect = @"
            SELECT f.file_transfer_id_pk as file_transfer_id, f.created 
            FROM broker.file_transfer f 
            INNER JOIN broker.actor a on a.actor_id_pk = f.sender_actor_id_fk 
            INNER JOIN LATERAL (SELECT fs.file_transfer_status_description_id_fk FROM broker.file_transfer_status fs where fs.file_transfer_id_fk = f.file_transfer_id_pk ORDER BY fs.file_transfer_status_id_pk desc LIMIT 1 ) AS filetransferstatus ON true
            WHERE a.actor_external_id = @actorExternalId AND resource_id = @resourceId
            {0}
            {1}";

        bool includeRecipient = fileTransferSearch.Role == SearchRole.Both || fileTransferSearch.Role == SearchRole.Recipient;
        bool includeSender = fileTransferSearch.Role == SearchRole.Both || fileTransferSearch.Role == SearchRole.Sender;


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

        string orderDirection = fileTransferSearch.OrderAscending ?? "DESC";

        var selects = new List<string>();
        if (includeRecipient) selects.Add(recipientSelect);
        if (includeSender) selects.Add(senderSelect);

        string commandString = string.Join("\nUNION\n\n", selects) + @"

            ORDER BY created {2}
            LIMIT 100;";

        commandString = string.Format(commandString, statusCondition, dateCondition, orderDirection);


        await using var command = dataSource.CreateCommand(commandString);
        command.Parameters.AddWithValue("@actorId", fileTransferSearch.Actor.ActorId);
        command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
        command.Parameters.AddWithValue("@actorExternalId", fileTransferSearch.Actor.ActorExternalId);
        if (fileTransferSearch.From.HasValue)
            command.Parameters.AddWithValue("@From", fileTransferSearch.From);
        if (fileTransferSearch.To.HasValue)
            command.Parameters.AddWithValue("@To", fileTransferSearch.To);
        if (fileTransferSearch.Status.HasValue)
            command.Parameters.AddWithValue("@fileTransferStatus", (int)fileTransferSearch.Status);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var files = new List<Guid>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(0);
                files.Add(fileTransferId);
            }

            return files;
        }, cancellationToken);
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
            ORDER BY created {2}
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

        string orderDirection = fileTransferSearch.OrderAscending ?? "DESC";
        commandString = string.Format(commandString, statusCondition, dateCondition, orderDirection);

        await using var command = dataSource.CreateCommand(commandString);
        command.Parameters.AddWithValue("@recipientId", fileTransferSearch.Actor.ActorId);
        command.Parameters.AddWithValue("@resourceId", fileTransferSearch.ResourceId);
        command.Parameters.AddWithValue("@recipientFileStatus", (int)fileTransferSearch.RecipientStatus);

        if (fileTransferSearch.From.HasValue)
            command.Parameters.AddWithValue("@from", fileTransferSearch.From);
        if (fileTransferSearch.To.HasValue)
            command.Parameters.AddWithValue("@to", fileTransferSearch.To);
        if (fileTransferSearch.Status.HasValue)
            command.Parameters.AddWithValue("@fileStatus", (int)fileTransferSearch.Status);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var files = new List<Guid>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(0);
                files.Add(fileTransferId);
            }

            return files;
        }, cancellationToken);
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
            await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
        }
    }

    private async Task<Dictionary<string, string>> GetMetadata(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT key, value FROM broker.file_transfer_property WHERE file_transfer_id_fk = @filetransferId");
        command.Parameters.AddWithValue("@filetransferId", fileTransferId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var metadata = new Dictionary<string, string>();
            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                metadata.Add(reader.GetString(reader.GetOrdinal("key")), reader.GetString(reader.GetOrdinal("value")));
            }
            return metadata;
        }, cancellationToken);
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
            await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
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
            await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
        }
    }

    public async Task<(List<FileTransferEntity> FileTransfers, Dictionary<Guid, string> ServiceOwnerIds)> GetFileTransfersForReportWithServiceOwnerIds(CancellationToken cancellationToken)
    {
        // Optimized query: Get all file transfers with sender, recipients, and service owner ID in one query (no N+1 problem)
        // Using DISTINCT to handle potential duplicates (same recipient can have multiple status changes)
        // JOIN with altinn_resource to get service_owner_id_fk directly, avoiding extra lookups
        const string query = @"
            SELECT DISTINCT ON (f.file_transfer_id_pk, recipient.actor_id_pk)
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
                sender.actor_external_id as sender_actor_external_id,
                recipient.actor_id_pk as recipient_actor_id_pk,
                recipient.actor_external_id as recipient_actor_external_id,
                afs.actor_file_transfer_status_description_id_fk,
                afs.actor_file_transfer_status_date,
                r.service_owner_id_fk as service_owner_id
            FROM broker.file_transfer f
            INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
            LEFT JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
            LEFT JOIN broker.actor_file_transfer_status afs ON afs.file_transfer_id_fk = f.file_transfer_id_pk
            LEFT JOIN broker.actor recipient ON recipient.actor_id_pk = afs.actor_id_fk
            ORDER BY f.file_transfer_id_pk, recipient.actor_id_pk NULLS LAST, afs.actor_file_transfer_status_date DESC";

        await using var command = dataSource.CreateCommand(query);
        
        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransfersDict = new Dictionary<Guid, FileTransferEntity>();
            var serviceOwnerIdsDict = new Dictionary<Guid, string>(); // Store service owner IDs from query
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk"));
                
                // Get or create file transfer entity
                if (!fileTransfersDict.TryGetValue(fileTransferId, out var fileTransfer))
                {
                    var senderActorId = reader.GetInt64(reader.GetOrdinal("sender_actor_id_fk"));
                    var senderActorExternalId = reader.GetString(reader.GetOrdinal("sender_actor_external_id"));
                    
                    // Store service owner ID from query (if available)
                    if (!reader.IsDBNull(reader.GetOrdinal("service_owner_id")))
                    {
                        serviceOwnerIdsDict[fileTransferId] = reader.GetString(reader.GetOrdinal("service_owner_id"));
                    }
                    
                    fileTransfer = new FileTransferEntity
                    {
                        FileTransferId = fileTransferId,
                        ResourceId = reader.GetString(reader.GetOrdinal("resource_id")),
                        FileName = reader.GetString(reader.GetOrdinal("filename")),
                        Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
                        Sender = new ActorEntity
                        {
                            ActorId = senderActorId,
                            ActorExternalId = senderActorExternalId
                        },
                        SendersFileTransferReference = reader.IsDBNull(reader.GetOrdinal("external_file_transfer_reference")) ? null : reader.GetString(reader.GetOrdinal("external_file_transfer_reference")),
                        Created = reader.GetDateTime(reader.GetOrdinal("created")),
                        ExpirationTime = reader.GetDateTime(reader.GetOrdinal("expiration_time")),
                        FileLocation = reader.IsDBNull(reader.GetOrdinal("file_location")) ? null : reader.GetString(reader.GetOrdinal("file_location")),
                        HangfireJobId = reader.IsDBNull(reader.GetOrdinal("hangfire_job_id")) ? null : reader.GetString(reader.GetOrdinal("hangfire_job_id")),
                        FileTransferSize = reader.IsDBNull(reader.GetOrdinal("file_transfer_size")) ? 0 : reader.GetInt64(reader.GetOrdinal("file_transfer_size")),
                        UseVirusScan = reader.GetBoolean(reader.GetOrdinal("use_virus_scan")),
                        RecipientCurrentStatuses = new List<ActorFileTransferStatusEntity>(),
                        FileTransferStatusEntity = new FileTransferStatusEntity
                        {
                            FileTransferId = fileTransferId,
                            Status = FileTransferStatus.UploadStarted, // Default, actual status not needed for report
                            Date = reader.GetDateTime(reader.GetOrdinal("created"))
                        },
                        FileTransferStatusChanged = reader.GetDateTime(reader.GetOrdinal("created")),
                        PropertyList = new Dictionary<string, string>()
                    };
                    
                    fileTransfersDict[fileTransferId] = fileTransfer;
                }
                
                // Add recipient if present (check for duplicates)
                if (!reader.IsDBNull(reader.GetOrdinal("recipient_actor_id_pk")))
                {
                    var recipientActorId = reader.GetInt64(reader.GetOrdinal("recipient_actor_id_pk"));
                    var recipientActorExternalId = reader.GetString(reader.GetOrdinal("recipient_actor_external_id"));
                    var recipientStatus = reader.GetInt32(reader.GetOrdinal("actor_file_transfer_status_description_id_fk"));
                    var recipientDate = reader.GetDateTime(reader.GetOrdinal("actor_file_transfer_status_date"));
                    
                    // Check if this recipient already exists (avoid duplicates)
                    if (!fileTransfer.RecipientCurrentStatuses.Any(r => r.Actor.ActorId == recipientActorId))
                    {
                        fileTransfer.RecipientCurrentStatuses.Add(new ActorFileTransferStatusEntity
                        {
                            FileTransferId = fileTransferId,
                            Actor = new ActorEntity
                            {
                                ActorId = recipientActorId,
                                ActorExternalId = recipientActorExternalId
                            },
                            Status = (ActorFileTransferStatus)recipientStatus,
                            Date = recipientDate
                        });
                    }
                }
            }
            
            return (fileTransfersDict.Values.ToList(), serviceOwnerIdsDict);
        }, cancellationToken);
    }


    public async Task<List<Guid>> GetFileTransfersByResourceId(string resourceId, DateTimeOffset minAge, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT file_transfer_id_pk " +
            "FROM broker.file_transfer " +
            "WHERE resource_id = @resourceId AND created < @minAge");

        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@minAge", minAge);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransferIds = new List<Guid>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk"));
                fileTransferIds.Add(fileTransferId);
            }

            return fileTransferIds;
        }, cancellationToken);
    }

    public async Task<List<Guid>> GetFileTransfersByPropertyTag(string resourceId, string propertyKey, string propertyValue, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT f.file_transfer_id_pk " +
            "FROM broker.file_transfer f " +
            "INNER JOIN broker.file_transfer_property p ON p.file_transfer_id_fk = f.file_transfer_id_pk " +
            "WHERE f.resource_id = @resourceId AND p.key = @propertyKey AND p.value = @propertyValue");

        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@propertyKey", propertyKey);
        command.Parameters.AddWithValue("@propertyValue", propertyValue);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransferIds = new List<Guid>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_pk"));
                fileTransferIds.Add(fileTransferId);
            }

            return fileTransferIds;
        }, cancellationToken);
    }

    public async Task<int> HardDeleteFileTransfersByIds(IEnumerable<Guid> fileTransferIds, CancellationToken cancellationToken)
    {
        var idsArray = fileTransferIds.ToArray();
        if (idsArray.Length == 0)
        {
            return 0;
        }
        if (idsArray.Length > 1000) //Safety margin
        {
            throw new ArgumentException($"Too many file transfers to delete. Total file transfers in requested hard delete: {idsArray.Length}", nameof(fileTransferIds));
        }

        await using var command = dataSource.CreateCommand(
            "DELETE FROM broker.file_transfer " +
            "WHERE file_transfer_id_pk = ANY(@fileTransferIds)");

        command.Parameters.Add(new NpgsqlParameter("@fileTransferIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = idsArray
        });

        return await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task<List<AggregatedDailySummaryData>> GetAggregatedDailySummaryData(CancellationToken cancellationToken)
    {
        const string query = @"
            WITH latest_recipient_status AS (
                SELECT DISTINCT ON (afs.file_transfer_id_fk, afs.actor_id_fk)
                    afs.file_transfer_id_fk,
                    afs.actor_id_fk,
                    afs.actor_file_transfer_status_description_id_fk,
                    afs.actor_file_transfer_status_date
                FROM broker.actor_file_transfer_status afs
                ORDER BY afs.file_transfer_id_fk, afs.actor_id_fk, afs.actor_file_transfer_status_date DESC, afs.actor_file_transfer_status_id_pk DESC
            )
            SELECT 
                DATE(f.created) as report_date,
                EXTRACT(YEAR FROM f.created)::int as year,
                EXTRACT(MONTH FROM f.created)::int as month,
                EXTRACT(DAY FROM f.created)::int as day,
                COALESCE(r.service_owner_id_fk, 'unknown') as service_owner_id,
                COALESCE(f.resource_id, 'unknown') as resource_id,
                COALESCE(recipient.actor_external_id, 'unknown') as recipient_id,
                CASE 
                    WHEN recipient.actor_external_id IS NOT NULL 
                        AND COALESCE(SPLIT_PART(recipient.actor_external_id, ':', -1), recipient.actor_external_id) ~ '^\d{9}$' THEN 1
                    WHEN recipient.actor_external_id IS NOT NULL 
                        AND COALESCE(SPLIT_PART(recipient.actor_external_id, ':', -1), recipient.actor_external_id) ~ '^\d{11}$' THEN 0
                    ELSE 2
                END as recipient_type,
                1 as altinn_version,
                COUNT(*)::int as message_count,
                0::bigint as database_storage_bytes,
                COALESCE(SUM(f.file_transfer_size), 0)::bigint as attachment_storage_bytes
            FROM broker.file_transfer f
            INNER JOIN broker.actor sender ON sender.actor_id_pk = f.sender_actor_id_fk
            LEFT JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
            LEFT JOIN latest_recipient_status lrs ON lrs.file_transfer_id_fk = f.file_transfer_id_pk
            LEFT JOIN broker.actor recipient ON recipient.actor_id_pk = lrs.actor_id_fk
            GROUP BY 
                DATE(f.created),
                EXTRACT(YEAR FROM f.created),
                EXTRACT(MONTH FROM f.created),
                EXTRACT(DAY FROM f.created),
                COALESCE(r.service_owner_id_fk, 'unknown'),
                COALESCE(f.resource_id, 'unknown'),
                COALESCE(recipient.actor_external_id, 'unknown'),
                CASE 
                    WHEN recipient.actor_external_id IS NOT NULL 
                        AND COALESCE(SPLIT_PART(recipient.actor_external_id, ':', -1), recipient.actor_external_id) ~ '^\d{9}$' THEN 1
                    WHEN recipient.actor_external_id IS NOT NULL 
                        AND COALESCE(SPLIT_PART(recipient.actor_external_id, ':', -1), recipient.actor_external_id) ~ '^\d{11}$' THEN 0
                    ELSE 2
                END
            ORDER BY 
                report_date,
                service_owner_id,
                resource_id,
                recipient_type,
                altinn_version";

        await using var command = dataSource.CreateCommand(query);
        command.CommandTimeout = 600;
        
        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var aggregatedData = new List<AggregatedDailySummaryData>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            while (await reader.ReadAsync(ct))
            {
                aggregatedData.Add(new AggregatedDailySummaryData
                {
                    Date = reader.GetDateTime(reader.GetOrdinal("report_date")),
                    Year = reader.GetInt32(reader.GetOrdinal("year")),
                    Month = reader.GetInt32(reader.GetOrdinal("month")),
                    Day = reader.GetInt32(reader.GetOrdinal("day")),
                    ServiceOwnerId = reader.IsDBNull(reader.GetOrdinal("service_owner_id")) 
                        ? "unknown" 
                        : reader.GetString(reader.GetOrdinal("service_owner_id")),
                    ResourceId = reader.IsDBNull(reader.GetOrdinal("resource_id")) 
                        ? "unknown" 
                        : reader.GetString(reader.GetOrdinal("resource_id")),
                    RecipientId = reader.IsDBNull(reader.GetOrdinal("recipient_id")) 
                        ? "unknown" 
                        : reader.GetString(reader.GetOrdinal("recipient_id")),
                    RecipientType = reader.GetInt32(reader.GetOrdinal("recipient_type")),
                    AltinnVersion = reader.GetInt32(reader.GetOrdinal("altinn_version")),
                    MessageCount = reader.GetInt32(reader.GetOrdinal("message_count")),
                    DatabaseStorageBytes = reader.GetInt64(reader.GetOrdinal("database_storage_bytes")),
                    AttachmentStorageBytes = reader.GetInt64(reader.GetOrdinal("attachment_storage_bytes"))
                });
            }
            
            return aggregatedData;
        }, cancellationToken);
    }
}

