using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ServiceRepository : IServiceRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public ServiceRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ServiceEntity?> GetService(long id)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT * FROM broker.service WHERE service_id_pk = @id");
        command.Parameters.AddWithValue("@id", id);

        return await GetServiceFromQuery(command);
    }

    public async Task<ServiceEntity?> GetService(string organizationNumber)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT * FROM broker.service WHERE organization_number = @organizationNumber");
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);

        return await GetServiceFromQuery(command);
    }

    private async Task<ServiceEntity?> GetServiceFromQuery(NpgsqlCommand command)
    {
        using NpgsqlDataReader reader = command.ExecuteReader();
        ServiceEntity? service = null;
        if (reader.Read())
        {
            service = new ServiceEntity
            {
                Id = reader.GetInt64(reader.GetOrdinal("service_id_pk")),
                Created = reader.GetDateTime(reader.GetOrdinal("created")),
                OrganizationNumber = reader.GetString(reader.GetOrdinal("organization_number")),
                ServiceOwnerId = reader.GetString(reader.GetOrdinal("service_owner_id_fk"))
            };
        }
        return service;
    }

    public async Task<long> InitializeService(string serviceOwnerId, string organizationNumber)
    {
        NpgsqlCommand command = await _connectionProvider.CreateCommand(
                    "INSERT INTO broker.service (created, organization_number, service_owner_id_fk) " +
                    "VALUES (@created, @organizationNumber, @serviceOwnerId) " +
                    "RETURNING service_id_pk");
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);

        return (long)command.ExecuteScalar()!;
    }
}

