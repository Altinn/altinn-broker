using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ServiceOwnerRepository(NpgsqlDataSource dataSource) : IServiceOwnerRepository
{
    public async Task<ServiceOwnerEntity?> GetServiceOwner(string serviceOwnerId)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT service_owner_id_pk, service_owner_name, " +
            "storage_provider_id_pk, created, resource_name, storage_provider_type " +
            "FROM broker.service_owner " +
            "LEFT JOIN broker.storage_provider sp on sp.service_owner_id_fk = service_owner_id_pk " +
            "WHERE service_owner_id_pk = @serviceOwnerId " +
            "ORDER BY created desc");
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);

        using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        ServiceOwnerEntity? serviceOwner = null;
        while (reader.Read())
        {
            serviceOwner = new ServiceOwnerEntity
            {
                Id = reader.GetString(reader.GetOrdinal("service_owner_id_pk")),
                Name = reader.GetString(reader.GetOrdinal("service_owner_name")),
                StorageProvider = reader.IsDBNull(reader.GetOrdinal("storage_provider_id_pk")) ? null : new StorageProviderEntity()
                {
                    Created = reader.GetDateTime(reader.GetOrdinal("created")),
                    Id = reader.GetInt64(reader.GetOrdinal("storage_provider_id_pk")),
                    ResourceName = reader.GetString(reader.GetOrdinal("resource_name")),
                    Type = Enum.Parse<StorageProviderType>(reader.GetString(reader.GetOrdinal("storage_provider_type")))
                }
            };
        }

        return serviceOwner;
    }

    public async Task InitializeServiceOwner(string sub, string name)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name) " +
            "VALUES (@sub, @name)");
        command.Parameters.AddWithValue("@sub", sub);
        command.Parameters.AddWithValue("@name", name);
        var commandText = command.CommandText;
        command.ExecuteNonQuery();


    }

    public async Task InitializeStorageProvider(string sub, string resourceName, StorageProviderType storageType)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO broker.storage_provider (created, resource_name, storage_provider_type, service_owner_id_fk) " +
            "VALUES (NOW(), @resourceName, @storageType, @serviceOwnerId)");
        command.Parameters.AddWithValue("@resourceName", resourceName);
        command.Parameters.AddWithValue("@storageType", storageType.ToString());
        command.Parameters.AddWithValue("@serviceOwnerId", sub);
        command.ExecuteNonQuery();
    }
}

