using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ServiceOwnerRepository : IServiceOwnerRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public ServiceOwnerRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ServiceOwnerEntity?> GetServiceOwner(string sub)
    {
        var connection = await _connectionProvider.GetConnectionAsync();
        using var command = new NpgsqlCommand(
            "SELECT service_owner_sub_pk, service_owner_name, azure_storage_account_connection_string " +
            "FROM broker.service_owner WHERE service_owner_sub_pk = @sub",
            connection);
        command.Parameters.AddWithValue("@sub", sub);

        using NpgsqlDataReader reader = command.ExecuteReader();
        ServiceOwnerEntity? serviceOwner = null;
        while (reader.Read())
        {
            serviceOwner = new ServiceOwnerEntity
            {
                Id = reader.GetString(reader.GetOrdinal("service_owner_sub_pk")),
                Name = reader.GetString(reader.GetOrdinal("service_owner_name")),
                StorageAccountConnectionString = reader.GetString(reader.GetOrdinal("azure_storage_account_connection_string"))
            };
        }

        return serviceOwner;
    }

    public async Task InitializeServiceOwner(string sub, string name)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.service_owner (service_owner_sub_pk, service_owner_name) " +
            "VALUES (@sub, @name)", connection))
        {
            command.Parameters.AddWithValue("@sub", sub);
            command.Parameters.AddWithValue("@name", name);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }
    }
}
