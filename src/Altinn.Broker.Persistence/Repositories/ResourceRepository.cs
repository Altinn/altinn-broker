using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ResourceRepository : IResourceRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAltinnResourceRepository _altinnResourceRepository;
    public ResourceRepository(NpgsqlDataSource dataSource, IAltinnResourceRepository altinnResourceRepository)
    {
        _dataSource = dataSource;
        _altinnResourceRepository = altinnResourceRepository;
    }

    public async Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT resource_id_pk, organization_number, max_file_transfer_size, file_transfer_time_to_live, created, service_owner_id_fk " +
            "FROM broker.altinn_resource " +
            "WHERE resource_id_pk = @resourceId " +
            "ORDER BY created desc");
        command.Parameters.AddWithValue("@resourceId", resourceId);

        using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        ResourceEntity? resource = null;
        while (reader.Read())
        {
            resource = new ResourceEntity
            {
                Id = reader.GetString(reader.GetOrdinal("resource_id_pk")),
                OrganizationNumber = reader.GetString(reader.GetOrdinal("organization_number")),
                MaxFileTransferSize = reader.IsDBNull(reader.GetOrdinal("max_file_transfer_size")) ? null : reader.GetInt64(reader.GetOrdinal("max_file_transfer_size")),
                FileTransferTimeToLive = reader.IsDBNull(reader.GetOrdinal("file_transfer_time_to_live")) ? null : reader.GetTimeSpan(reader.GetOrdinal("file_transfer_time_to_live")),
                Created = reader.GetDateTime(reader.GetOrdinal("created")),
                ServiceOwnerId = reader.GetString(reader.GetOrdinal("service_owner_id_fk"))
            };
        }
        if (resource is null)
        {
            resource = await _altinnResourceRepository.GetResource(resourceId, cancellationToken);
            if (resource is null || string.IsNullOrWhiteSpace(resource.ServiceOwnerId))
            {
                return null;
            }
            await CreateResource(resource, cancellationToken);
        }
        return resource;
    }
    public async Task CreateResource(ResourceEntity resource, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "INSERT INTO broker.altinn_resource (resource_id_pk, organization_number, max_file_transfer_size, file_transfer_time_to_live, created, service_owner_id_fk) " +
            "VALUES (@resourceId, @organizationNumber, @maxFileTransferSize, @fileTransferTimeToLive, NOW(), @serviceOwnerId)");
        command.Parameters.AddWithValue("@resourceId", resource.Id);
        command.Parameters.AddWithValue("@organizationNumber", resource.OrganizationNumber ?? "");
        command.Parameters.AddWithValue("@maxFileTransferSize", resource.MaxFileTransferSize == null ? DBNull.Value : resource.MaxFileTransferSize);
        command.Parameters.AddWithValue("@fileTransferTimeToLive", resource.FileTransferTimeToLive is null ? DBNull.Value : resource.FileTransferTimeToLive.Value);
        command.Parameters.AddWithValue("@serviceOwnerId", resource.ServiceOwnerId);
        command.ExecuteNonQuery();
    }
    public async Task UpdateMaxFileTransferSize(string resource, long maxSize, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET max_file_transfer_size = @maxFileTransferSize " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resource);
        command.Parameters.AddWithValue("@maxFileTransferSize", maxSize);
        command.ExecuteNonQuery();
    }

    public async Task UpdateFileRetention(string resourceId, TimeSpan fileTransferTimeToLive, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET file_transfer_time_to_live = @fileTransferTimeToLive " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@fileTransferTimeToLive", fileTransferTimeToLive);
        command.ExecuteNonQuery();
    }
    public async Task UpdateDeleteFileTransferAfterAllRecipientsConfirmed(string resourceId, bool deleteFileTransferAfterAllRecipientsConfirmed, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET delete_file_transfer_after_all_recipients_confirmed = @deleteFileTransferAfterAllRecipientsConfirmed " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@deleteFileTransferAfterAllRecipientsConfirmed", deleteFileTransferAfterAllRecipientsConfirmed);
        command.ExecuteNonQuery();
    }
    public async Task UpdateDeleteFileTransferGracePeriod(string resourceId, TimeSpan deleteFileTransferGracePeriod, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET delete_file_transfer_grace_period = @deleteFileTransferGracePeriod " +
            "WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@deleteFileTransferGracePeriod", deleteFileTransferGracePeriod);
        command.ExecuteNonQuery();
    }
}
