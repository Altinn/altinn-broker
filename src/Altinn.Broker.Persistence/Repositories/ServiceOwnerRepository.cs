﻿using Altinn.Broker.Core.Domain;
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

    public async Task<ServiceOwnerEntity?> GetServiceOwner(string serviceOwnerId)
    {
        await using var command = await _connectionProvider.CreateCommand(
            "SELECT service_owner_id_pk, service_owner_name, file_transfer_time_to_live, " +
            "storage_provider_id_pk, created, resource_name, storage_provider_type " +
            "FROM broker.service_owner " +
            "LEFT JOIN broker.storage_provider sp on sp.service_owner_id_fk = service_owner_id_pk " +
            "WHERE service_owner_id_pk = @serviceOwnerId " +
            "ORDER BY created desc");
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);

        using NpgsqlDataReader reader = command.ExecuteReader();
        ServiceOwnerEntity? serviceOwner = null;
        while (reader.Read())
        {
            serviceOwner = new ServiceOwnerEntity
            {
                Id = reader.GetString(reader.GetOrdinal("service_owner_id_pk")),
                Name = reader.GetString(reader.GetOrdinal("service_owner_name")),
                FileTransferTimeToLive = reader.GetTimeSpan(reader.GetOrdinal("file_transfer_time_to_live")),
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
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name, file_transfer_time_to_live) " +
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
            "INSERT INTO broker.storage_provider (created, resource_name, storage_provider_type, service_owner_id_fk) " +
            "VALUES (NOW(), @resourceName, @storageType, @serviceOwnerId)"))
        {
            command.Parameters.AddWithValue("@resourceName", resourceName);
            command.Parameters.AddWithValue("@storageType", storageType.ToString());
            command.Parameters.AddWithValue("@serviceOwnerId", sub);
            command.ExecuteNonQuery();
        }
    }
    public async Task UpdateFileRetention(string sub, TimeSpan fileTransferTimeToLive)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();

        await using (var command = await _connectionProvider.CreateCommand(
            "UPDATE broker.service_owner " +
            "SET file_transfer_time_to_live = @fileTransferTimeToLive " +
            "WHERE service_owner_id_pk = @sub"))
        {
            command.Parameters.AddWithValue("@sub", sub);
            command.Parameters.AddWithValue("@fileTransferTimeToLive", fileTransferTimeToLive);
            command.ExecuteNonQuery();
        }
    }
}

