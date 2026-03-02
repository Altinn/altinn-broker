using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ResourceRepository(NpgsqlDataSource dataSource, IAltinnResourceRepository altinnResourceRepository, IServiceOwnerRepository serviceOwnerRepository, ExecuteDBCommandWithRetries commandExecutor) : IResourceRepository
{
    public async Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT resource_id_pk, organization_number, max_file_transfer_size, file_transfer_time_to_live, created, service_owner_id_fk, purge_file_transfer_after_all_recipients_confirmed, purge_file_transfer_grace_period, use_manifest_file_shim, external_service_code_legacy, external_service_edition_code_legacy, required_party " +
            "FROM broker.altinn_resource " +
            "WHERE resource_id_pk = @resourceId " +
            "ORDER BY created desc");
        command.Parameters.AddWithValue("@resourceId", resourceId);

        ResourceEntity? resource = null;
        
        await using (var reader = await commandExecutor.ExecuteWithRetry(command.ExecuteReaderAsync, cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                resource = new ResourceEntity
                {
                    Id = reader.GetString(reader.GetOrdinal("resource_id_pk")),
                    OrganizationNumber = reader.GetString(reader.GetOrdinal("organization_number")),
                    MaxFileTransferSize = reader.IsDBNull(reader.GetOrdinal("max_file_transfer_size")) ? null : reader.GetInt64(reader.GetOrdinal("max_file_transfer_size")),
                    FileTransferTimeToLive = reader.IsDBNull(reader.GetOrdinal("file_transfer_time_to_live")) ? null : reader.GetTimeSpan(reader.GetOrdinal("file_transfer_time_to_live")),
                    Created = reader.GetDateTime(reader.GetOrdinal("created")),
                    ServiceOwnerId = reader.GetString(reader.GetOrdinal("service_owner_id_fk")),
                    PurgeFileTransferAfterAllRecipientsConfirmed = reader.GetBoolean(reader.GetOrdinal("purge_file_transfer_after_all_recipients_confirmed")),
                    PurgeFileTransferGracePeriod = reader.IsDBNull(reader.GetOrdinal("purge_file_transfer_grace_period")) ? null : reader.GetTimeSpan(reader.GetOrdinal("purge_file_transfer_grace_period")),
                    UseManifestFileShim = reader.IsDBNull(reader.GetOrdinal("use_manifest_file_shim")) ? null : reader.GetBoolean(reader.GetOrdinal("use_manifest_file_shim")),
                    ExternalServiceCodeLegacy = reader.IsDBNull(reader.GetOrdinal("external_service_code_legacy")) ? null : reader.GetString(reader.GetOrdinal("external_service_code_legacy")),
                    ExternalServiceEditionCodeLegacy = reader.IsDBNull(reader.GetOrdinal("external_service_edition_code_legacy")) ? null : reader.GetInt32(reader.GetOrdinal("external_service_edition_code_legacy")),
                    RequiredParty = reader.IsDBNull(reader.GetOrdinal("required_party")) ? null : reader.GetBoolean(reader.GetOrdinal("required_party"))
                };
            }
        }
        return resource;
    }
    
    public async Task<ResourceEntity> CreateResource(ResourceEntity resource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO broker.altinn_resource (resource_id_pk, organization_number, max_file_transfer_size, file_transfer_time_to_live, created, service_owner_id_fk) " +
            "VALUES (@resourceId, @organizationNumber, @maxFileTransferSize, @fileTransferTimeToLive, NOW(), @serviceOwnerId)");
        command.Parameters.AddWithValue("@resourceId", resource.Id);
        command.Parameters.AddWithValue("@organizationNumber", resource.OrganizationNumber ?? "");
        command.Parameters.AddWithValue("@maxFileTransferSize", resource.MaxFileTransferSize == null ? DBNull.Value : resource.MaxFileTransferSize);
        command.Parameters.AddWithValue("@fileTransferTimeToLive", resource.FileTransferTimeToLive is null ? DBNull.Value : resource.FileTransferTimeToLive.Value);
        command.Parameters.AddWithValue("@serviceOwnerId", resource.ServiceOwnerId);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
        return resource;
    }
    
    public async Task UpdateMaxFileTransferSize(string resource, long maxSize, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET max_file_transfer_size = @maxFileTransferSize " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resource);
        command.Parameters.AddWithValue("@maxFileTransferSize", maxSize);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task UpdateFileRetention(string resourceId, TimeSpan fileTransferTimeToLive, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET file_transfer_time_to_live = @fileTransferTimeToLive " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@fileTransferTimeToLive", fileTransferTimeToLive);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
    
    public async Task UpdatePurgeFileTransferAfterAllRecipientsConfirmed(string resourceId, bool PurgeFileTransferAfterAllRecipientsConfirmed, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET purge_file_transfer_after_all_recipients_confirmed = @PurgeFileTransferAfterAllRecipientsConfirmed " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@PurgeFileTransferAfterAllRecipientsConfirmed", PurgeFileTransferAfterAllRecipientsConfirmed);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task UpdatePurgeFileTransferGracePeriod(string resourceId, TimeSpan PurgeFileTransferGracePeriod, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET purge_file_transfer_grace_period = @PurgeFileTransferGracePeriod " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@PurgeFileTransferGracePeriod", PurgeFileTransferGracePeriod);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task UpdateUseManifestFileShim(string resourceId, bool useManifestFileShim, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET use_manifest_file_shim = @UseManifestFileShim " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@UseManifestFileShim", useManifestFileShim);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
    
    public async Task UpdateExternalServiceCodeLegacy(string resourceId, string externalServiceCodeLegacy, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET external_service_code_legacy = @externalServiceCodeLegacy " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@externalServiceCodeLegacy", externalServiceCodeLegacy);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
    
    public async Task UpdateExternalServiceEditionCodeLegacy(string resourceId, int? externalServiceEditionCodeLegacy, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET external_service_edition_code_legacy = @externalServiceEditionCodeLegacy " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@externalServiceEditionCodeLegacy", (object?)externalServiceEditionCodeLegacy ?? DBNull.Value);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task UpdateRequiredParty(string resourceId, bool requiredParty, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET required_party = @requiredParty " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@requiredParty", requiredParty);
        
        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
}
