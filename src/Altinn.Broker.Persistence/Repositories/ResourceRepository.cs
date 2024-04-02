using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ResourceRepository : IResourceRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;
    private readonly IAltinnResourceRepository _altinnResourceRepository;
    public ResourceRepository(DatabaseConnectionProvider connectionProvider, IAltinnResourceRepository altinnResourceRepository)
    {
        _connectionProvider = connectionProvider;
        _altinnResourceRepository = altinnResourceRepository;
    }

    public async Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        await using var command = await _connectionProvider.CreateCommand(
            "SELECT resource_id_pk, organization_number, max_file_transfer_size, created, service_owner_id_fk " +
            "FROM broker.altinn_resource " +
            "WHERE resource_id_pk = @resourceId " +
            "ORDER BY created desc");
        command.Parameters.AddWithValue("@resourceId", resourceId);

        using NpgsqlDataReader reader = command.ExecuteReader();
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
            if (resource is null)
            {
                return null;
            }
            await CreateResource(resource, cancellationToken);
        }
        return resource;
    }
    public async Task CreateResource(ResourceEntity resource, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.altinn_resource (resource_id_pk, organization_number, max_file_transfer_size, created, service_owner_id_fk) " +
            "VALUES (@resourceId, @organizationNumber, @maxFileTransferSize, NOW(), @serviceOwnerId)"))
        {
            command.Parameters.AddWithValue("@resourceId", resource.Id);
            command.Parameters.AddWithValue("@organizationNumber", resource.OrganizationNumber ?? "");
            command.Parameters.AddWithValue("@maxFileTransferSize", resource.MaxFileTransferSize == null ? DBNull.Value : resource.MaxFileTransferSize);
            command.Parameters.AddWithValue("@serviceOwnerId", resource.ServiceOwnerId);
            command.ExecuteNonQuery();
        }
    }
    public async Task UpdateMaxFileTransferSize(string resource, long maxSize, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET max_file_transfer_size = @maxFileTransferSize " +
            "WHERE resource_id_pk = @resourceId"))
        {
            command.Parameters.AddWithValue("@resourceId", resource);
            command.Parameters.AddWithValue("@maxFileTransferSize", maxSize);
            command.ExecuteNonQuery();
        }
    }

    public async Task UpdateFileRetention(string resourceId, string fileTransferTimeToLive, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.altinn_resource " +
            "SET file_transfer_time_to_live = @fileTransferTimeToLive " +
            "WHERE resource_id_pk = @resourceId"))
        {
            command.Parameters.AddWithValue("@resourceId", resourceId);
            command.Parameters.AddWithValue("@fileTransferTimeToLive", fileTransferTimeToLive);
            command.ExecuteNonQuery();
        }
    }
}

