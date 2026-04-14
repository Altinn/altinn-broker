using Altinn.Broker.Core.Domain.Enums;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Broker.Tests.Helpers;

public class TestDataHelper(NpgsqlDataSource dataSource)
{
    public const string DefaultServiceOwnerId = "0192:991825827";
    public const string DefaultSenderExternalId = "0192:991825827";

    public async Task EnsureResource(string resourceId, string organizationNumber, string serviceOwnerId = DefaultServiceOwnerId)
    {
        await EnsureStorageProvider(serviceOwnerId);

        await using var command = dataSource.CreateCommand(
            @"INSERT INTO broker.altinn_resource (resource_id_pk, created, organization_number, service_owner_id_fk)
              VALUES (@resourceId, NOW(), @organizationNumber, @serviceOwnerId)
              ON CONFLICT (resource_id_pk) DO NOTHING");

        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> InsertFileTransfer(
        string resourceId,
        string serviceOwnerId = DefaultServiceOwnerId,
        DateTimeOffset? created = null,
        string senderExternalId = DefaultSenderExternalId,
        string fileName = "test.txt",
        string? externalReference = null,
        DateTimeOffset? expirationTime = null,
        bool useVirusScan = false)
    {
        var fileTransferId = Guid.NewGuid();
        var senderActorId = await EnsureActor(senderExternalId);
        var storageProviderId = await EnsureStorageProvider(serviceOwnerId);
        var createdValue = created ?? DateTimeOffset.UtcNow;
        var expirationTimeValue = expirationTime ?? createdValue.AddHours(1);

        await using var command = dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer (
                    file_transfer_id_pk, resource_id, created, filename, checksum, file_transfer_size,
                    sender_actor_id_fk, external_file_transfer_reference, expiration_time, storage_provider_id_fk, use_virus_scan)
              VALUES (
                    @fileTransferId, @resourceId, @created, @fileName, NULL, 0,
                    @senderActorId, @externalReference, @expirationTime, @storageProviderId, @useVirusScan)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@created", createdValue.UtcDateTime);
        command.Parameters.AddWithValue("@fileName", fileName);
        command.Parameters.AddWithValue("@senderActorId", senderActorId);
        command.Parameters.AddWithValue("@externalReference", externalReference ?? $"ref-{fileTransferId}");
        command.Parameters.AddWithValue("@expirationTime", expirationTimeValue.UtcDateTime);
        command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
        command.Parameters.AddWithValue("@useVirusScan", useVirusScan);
        await command.ExecuteNonQueryAsync();

        return fileTransferId;
    }

    public async Task InsertActorStatus(Guid fileTransferId, string actorExternalId, ActorFileTransferStatus status, DateTimeOffset statusDate)
    {
        var actorId = await EnsureActor(actorExternalId);

        await using var command = dataSource.CreateCommand(
            @"INSERT INTO broker.actor_file_transfer_status (
                    actor_id_fk, file_transfer_id_fk, actor_file_transfer_status_description_id_fk, actor_file_transfer_status_date)
              VALUES (@actorId, @fileTransferId, @status, @statusDate)");

        command.Parameters.AddWithValue("@actorId", actorId);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@status", (int)status);
        command.Parameters.AddWithValue("@statusDate", statusDate.UtcDateTime);
        await command.ExecuteNonQueryAsync();
    }

    public async Task InsertFileTransferStatus(Guid fileTransferId, FileTransferStatus status, DateTimeOffset statusDate, string? detailedStatus = null)
    {
        await using var command = dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer_status (
                    file_transfer_id_fk, file_transfer_status_description_id_fk, file_transfer_status_date, file_transfer_status_detailed_description)
              VALUES (@fileTransferId, @status, @statusDate, @detailedStatus)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@status", (int)status);
        command.Parameters.AddWithValue("@statusDate", statusDate.UtcDateTime);
        command.Parameters.AddWithValue("@detailedStatus", (object?)detailedStatus ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task InsertProperty(Guid fileTransferId, string key, string value)
    {
        await using var command = dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer_property (file_transfer_id_fk, key, value)
              VALUES (@fileTransferId, @key, @value)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> EnsureActor(string actorExternalId)
    {
        await using var selectCommand = dataSource.CreateCommand(
            "SELECT actor_id_pk FROM broker.actor WHERE actor_external_id = @actorExternalId");
        selectCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        var existingActorId = await selectCommand.ExecuteScalarAsync();
        if (existingActorId is long actorId)
        {
            return actorId;
        }

        await using var insertCommand = dataSource.CreateCommand(
            "INSERT INTO broker.actor (actor_external_id) VALUES (@actorExternalId) RETURNING actor_id_pk");
        insertCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        return (long)(await insertCommand.ExecuteScalarAsync())!;
    }

    public async Task<long> EnsureStorageProvider(string serviceOwnerId = DefaultServiceOwnerId)
    {
        await using var selectCommand = dataSource.CreateCommand(
            "SELECT storage_provider_id_pk FROM broker.storage_provider WHERE service_owner_id_fk = @serviceOwnerId LIMIT 1");
        selectCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        var existingStorageProviderId = await selectCommand.ExecuteScalarAsync();
        if (existingStorageProviderId is long storageProviderId)
        {
            return storageProviderId;
        }

        await using var insertServiceOwnerCommand = dataSource.CreateCommand(
            @"INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name)
              VALUES (@serviceOwnerId, @serviceOwnerName)
              ON CONFLICT DO NOTHING");
        insertServiceOwnerCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        insertServiceOwnerCommand.Parameters.AddWithValue("@serviceOwnerName", $"Service owner {serviceOwnerId}");
        await insertServiceOwnerCommand.ExecuteNonQueryAsync();

        await using var insertStorageProviderCommand = dataSource.CreateCommand(
            @"INSERT INTO broker.storage_provider (
                    service_owner_id_fk, created, storage_provider_type, resource_name, active)
              VALUES (@serviceOwnerId, NOW(), @storageProviderType, @resourceName, true)
              RETURNING storage_provider_id_pk");
        insertStorageProviderCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        insertStorageProviderCommand.Parameters.AddWithValue("@storageProviderType", "Altinn3Azure");
        insertStorageProviderCommand.Parameters.AddWithValue("@resourceName", $"stats-storage-{Guid.NewGuid():N}");

        return (long)(await insertStorageProviderCommand.ExecuteScalarAsync())!;
    }

    public async Task DeleteTestData(IReadOnlyCollection<string> resourceIds, IReadOnlyCollection<string> serviceOwnerIds)
    {
        if (resourceIds.Count > 0)
        {
            await using var deleteRollupByResourceCommand = dataSource.CreateCommand(
                @"DELETE FROM broker.monthly_statistics_rollup
                  WHERE resource_id = ANY(@resourceIds)");
            deleteRollupByResourceCommand.Parameters.Add(new NpgsqlParameter("@resourceIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = resourceIds.ToArray()
            });
            await deleteRollupByResourceCommand.ExecuteNonQueryAsync();

            await using var deleteFileTransfersCommand = dataSource.CreateCommand(
                @"DELETE FROM broker.file_transfer
                  WHERE resource_id = ANY(@resourceIds)");
            deleteFileTransfersCommand.Parameters.Add(new NpgsqlParameter("@resourceIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = resourceIds.ToArray()
            });
            await deleteFileTransfersCommand.ExecuteNonQueryAsync();

            await using var deleteResourcesCommand = dataSource.CreateCommand(
                @"DELETE FROM broker.altinn_resource
                  WHERE resource_id_pk = ANY(@resourceIds)");
            deleteResourcesCommand.Parameters.Add(new NpgsqlParameter("@resourceIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = resourceIds.ToArray()
            });
            await deleteResourcesCommand.ExecuteNonQueryAsync();
        }

        if (serviceOwnerIds.Count > 0)
        {
            await using var deleteRollupByServiceOwnerCommand = dataSource.CreateCommand(
                @"DELETE FROM broker.monthly_statistics_rollup
                  WHERE service_owner_id = ANY(@serviceOwnerIds)");
            deleteRollupByServiceOwnerCommand.Parameters.Add(new NpgsqlParameter("@serviceOwnerIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = serviceOwnerIds.ToArray()
            });
            await deleteRollupByServiceOwnerCommand.ExecuteNonQueryAsync();

            await using var deleteStorageProvidersCommand = dataSource.CreateCommand(
                @"DELETE FROM broker.storage_provider
                  WHERE service_owner_id_fk = ANY(@serviceOwnerIds)");
            deleteStorageProvidersCommand.Parameters.Add(new NpgsqlParameter("@serviceOwnerIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = serviceOwnerIds.ToArray()
            });
            await deleteStorageProvidersCommand.ExecuteNonQueryAsync();

            await using var deleteServiceOwnersCommand = dataSource.CreateCommand(
                @"DELETE FROM broker.service_owner
                  WHERE service_owner_id_pk = ANY(@serviceOwnerIds)");
            deleteServiceOwnersCommand.Parameters.Add(new NpgsqlParameter("@serviceOwnerIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = serviceOwnerIds.ToArray()
            });
            await deleteServiceOwnersCommand.ExecuteNonQueryAsync();
        }
    }
}
