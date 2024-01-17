using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class ResourceOwnerRepository : IResourceOwnerRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public ResourceOwnerRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<ResourceOwnerEntity?> GetResourceOwner(string resourceOwnerId)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT resource_owner_id_pk, resource_owner_name, file_time_to_live, " +
            "storage_provider_id_pk, created, resource_name, storage_provider_type " +
            "FROM broker.resource_owner " +
            "LEFT JOIN broker.storage_provider sp on sp.resource_owner_id_fk = resource_owner_id_pk " +
            "WHERE resource_owner_id_pk = @resourceOwnerId " +
            "ORDER BY created desc");
        command.Parameters.AddWithValue("@resourceOwnerId", resourceOwnerId);

        using NpgsqlDataReader reader = command.ExecuteReader();
        ResourceOwnerEntity? resourceOwner = null;
        while (reader.Read())
        {
            resourceOwner = new ResourceOwnerEntity
            {
                Id = reader.GetString(reader.GetOrdinal("resource_owner_id_pk")),
                Name = reader.GetString(reader.GetOrdinal("resource_owner_name")),
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

        return resourceOwner;
    }

    public async Task InitializeResourceOwner(string sub, string name, TimeSpan fileTimeToLive)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.resource_owner (resource_owner_id_pk, resource_owner_name, file_time_to_live) " +
            "VALUES (@sub, @name, @fileTimeToLive)"))
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
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.storage_provider (created, resource_name, storage_provider_type, resource_owner_id_fk) " +
            "VALUES (NOW(), @resourceName, @storageType, @resourceOwnerId)"))
        {
            command.Parameters.AddWithValue("@resourceName", resourceName);
            command.Parameters.AddWithValue("@storageType", storageType.ToString());
            command.Parameters.AddWithValue("@resourceOwnerId", sub);
            command.ExecuteNonQuery();
        }
    }
}

