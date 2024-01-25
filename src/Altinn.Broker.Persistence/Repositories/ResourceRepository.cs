using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ResourceRepository : IResourceRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public ResourceRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ResourceEntity?> GetResource(string resourceId)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT * FROM broker.resource WHERE resource_id_pk = @resourceId");
        command.Parameters.AddWithValue("@resourceId", resourceId);

        return await GetResourceFromQuery(command);
    }

    public async Task<List<string>> SearchResources(string resourceOwnerOrgNo)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT resource_id_pk FROM broker.resource WHERE resource_owner_id_fk = @resourceOwnerOrgNo");
        command.Parameters.AddWithValue("@resourceOwnerOrgNo", resourceOwnerOrgNo);

        var services = new List<string>();
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var serviceExternalId = reader.GetString(0);
                services.Add(serviceExternalId);
            }
        }
        return services;
    }

    private async Task<ResourceEntity?> GetResourceFromQuery(NpgsqlCommand command)
    {
        using NpgsqlDataReader reader = command.ExecuteReader();
        ResourceEntity? service = null;
        if (reader.Read())
        {
            service = new ResourceEntity
            {
                Id = reader.GetString(reader.GetOrdinal("resource_id_pk")),
                Created = reader.GetDateTime(reader.GetOrdinal("created")),
                OrganizationNumber = reader.GetString(reader.GetOrdinal("organization_number")),
                ResourceOwnerId = reader.GetString(reader.GetOrdinal("resource_owner_id_fk"))
            };
        }
        return service;
    }

    public async Task InitializeResource(string resourceOwnerId, string organizationNumber, string resourceId)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "INSERT INTO broker.resource (resource_id_pk, created, organization_number, resource_owner_id_fk) " +
                    "VALUES (@resourceId, @created, @organizationNumber, @resourceOwnerId) " +
                    "RETURNING resource_id_pk");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@resourceOwnerId", resourceOwnerId);
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);

        command.ExecuteNonQuery();
    }
}

