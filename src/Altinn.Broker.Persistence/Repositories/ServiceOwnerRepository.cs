using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Microsoft.Azure.Management.Storage.Models;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ServiceOwnerRepository : IServiceOwnerRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public ServiceOwnerRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ServiceOwnerEntity?> GetServiceOwner(string serviceOwnerId)
    {
        using var connection = await _connectionProvider.GetConnectionAsync();
        using var command = new NpgsqlCommand(
            "SELECT service_owner_id_pk, service_owner_name, file_time_to_live, " +
            "storage_provider_id_pk, created, resource_name, storage_provider_type " +
            "FROM broker.service_owner " +
            "LEFT JOIN broker.storage_provider sp on sp.service_owner_id_fk = service_owner_id_pk " +
            "WHERE service_owner_id_pk = @serviceOwnerId " +
            "ORDER BY created desc",
            connection);
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);

        using NpgsqlDataReader reader = command.ExecuteReader();
        ServiceOwnerEntity? serviceOwner = null;
        while (reader.Read())
        {
            serviceOwner = new ServiceOwnerEntity
            {
                Id = reader.GetString(reader.GetOrdinal("service_owner_id_pk")),
                Name = reader.GetString(reader.GetOrdinal("service_owner_name")),
                FileTimeToLive = reader.GetTimeSpan(reader.GetOrdinal("file_time_to_live")),
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

    public async Task InitializeServiceOwner(string sub, string name, TimeSpan fileTimeToLive)
    {
        using var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name, file_time_to_live) " +
            "VALUES (@sub, @name, @fileTimeToLive)", connection))
        {
            command.Parameters.AddWithValue("@sub", sub);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@fileTimeToLive", fileTimeToLive);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }


    }

    public async Task InitializeStorageProvider(string sub, string resourceName, StorageProviderType storageType)
    {
        using var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.storage_provider (created, resource_name, storage_provider_type, service_owner_id_fk) " +
            "VALUES (NOW(), @resourceName, @storageType, @serviceOwnerId)", connection))
        {
            command.Parameters.AddWithValue("@resourceName", resourceName);
            command.Parameters.AddWithValue("@storageType", storageType.ToString());
            command.Parameters.AddWithValue("@serviceOwnerId", sub);
            command.ExecuteNonQuery();
        }
    }
}

