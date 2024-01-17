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

    public async Task<ResourceEntity?> GetResource(long id)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT * FROM broker.service WHERE service_id_pk = @id");
        command.Parameters.AddWithValue("@id", id);

        return await GetServiceFromQuery(command);
    }

    public async Task<ResourceEntity?> GetResource(string clientId)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT * FROM broker.service WHERE client_id = @clientId");
        command.Parameters.AddWithValue("@clientId", clientId);

        return await GetServiceFromQuery(command);
    }

    public async Task<List<string>> SearchResources(string resourceOwnerOrgNo)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT client_id FROM broker.service WHERE resource_owner_id_fk = @resourceOwnerOrgNo");
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

    private async Task<ResourceEntity?> GetServiceFromQuery(NpgsqlCommand command)
    {
        using NpgsqlDataReader reader = command.ExecuteReader();
        ResourceEntity? service = null;
        if (reader.Read())
        {
            service = new ResourceEntity
            {
                Id = reader.GetInt64(reader.GetOrdinal("service_id_pk")),
                ClientId = reader.GetString(reader.GetOrdinal("client_id")),
                Created = reader.GetDateTime(reader.GetOrdinal("created")),
                OrganizationNumber = reader.GetString(reader.GetOrdinal("organization_number")),
                ResourceOwnerId = reader.GetString(reader.GetOrdinal("resource_owner_id_fk"))
            };
        }
        return service;
    }

    public async Task<long> InitializeResource(string resourceOwnerId, string organizationNumber, string clientId)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "INSERT INTO broker.service (created, client_id, organization_number, resource_owner_id_fk) " +
                    "VALUES (@created, @clientId, @organizationNumber, @resourceOwnerId) " +
                    "RETURNING service_id_pk");
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@resourceOwnerId", resourceOwnerId);
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);
        command.Parameters.AddWithValue("@clientId", clientId);

        return (long)command.ExecuteScalar()!;
    }
}

