using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class ActorFileTransferStatusRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IActorFileTransferStatusRepository _repository;
    private readonly NpgsqlDataSource _dataSource;

    public ActorFileTransferStatusRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _repository = factory.Services.GetRequiredService<IActorFileTransferStatusRepository>();
        _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
    }

    [Fact]
    public async Task InsertActorFileTransferStatus_FirstStatus_UpdatesDenormalizedTable()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var actorExternalId = "0192:986252932";

        // Act
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, actorExternalId, cancellationToken: default);

        // Assert
        await using var command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id, latest_actor_status_date 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((int)ActorFileTransferStatus.Initialized, reader.GetInt32(reader.GetOrdinal("latest_actor_status_id")));
        Assert.NotNull(reader.GetValue(reader.GetOrdinal("latest_actor_status_date")));
    }

    [Fact]
    public async Task InsertActorFileTransferStatus_NewerStatus_UpdatesDenormalizedTable()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var actorExternalId = "0192:986252932";
        
        // Insert initial status via repository
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, actorExternalId, cancellationToken: default);
        
        // Get the initial date
        await using var getInitialCommand = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_date 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        getInitialCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        getInitialCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        await using var initialReader = await getInitialCommand.ExecuteReaderAsync();
        await initialReader.ReadAsync();
        var initialDate = initialReader.GetDateTime(initialReader.GetOrdinal("latest_actor_status_date"));
        await initialReader.CloseAsync();

        // Wait to ensure different timestamp
        await Task.Delay(100);

        // Act - Insert newer status
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadConfirmed, actorExternalId, cancellationToken: default);

        // Assert
        await using var command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id, latest_actor_status_date 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((int)ActorFileTransferStatus.DownloadConfirmed, reader.GetInt32(reader.GetOrdinal("latest_actor_status_id")));
        var latestDate = reader.GetDateTime(reader.GetOrdinal("latest_actor_status_date"));
        Assert.True(latestDate > initialDate);
    }

    [Fact]
    public async Task InsertActorFileTransferStatus_StatusSequence_AlwaysKeepsLatest()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var actorExternalId = "0192:986252932";
        
        // Insert statuses in sequence - each should update the denormalized table
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadConfirmed, actorExternalId, cancellationToken: default);
        
        // Get the first status
        await using var getFirstCommand = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id, latest_actor_status_date 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        getFirstCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        getFirstCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        await using var firstReader = await getFirstCommand.ExecuteReaderAsync();
        await firstReader.ReadAsync();
        var firstStatusId = firstReader.GetInt32(firstReader.GetOrdinal("latest_actor_status_id"));
        var firstDate = firstReader.GetDateTime(firstReader.GetOrdinal("latest_actor_status_date"));
        await firstReader.CloseAsync();
        
        Assert.Equal((int)ActorFileTransferStatus.DownloadConfirmed, firstStatusId);

        // Wait to ensure different timestamp
        await Task.Delay(100);

        // Act - Insert another status (should update since it's newer)
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, actorExternalId, cancellationToken: default);

        // Assert - Denormalized table should have the latest status (even though it's "lower" in the enum, it's newer in time)
        await using var command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id, latest_actor_status_date 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        // The latest status should be Initialized (the second one inserted) since it's newer
        Assert.Equal((int)ActorFileTransferStatus.Initialized, reader.GetInt32(reader.GetOrdinal("latest_actor_status_id")));
        var finalDate = reader.GetDateTime(reader.GetOrdinal("latest_actor_status_date"));
        Assert.True(finalDate > firstDate);
    }

    [Fact]
    public async Task InsertActorFileTransferStatus_MultipleStatuses_KeepsLatest()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var actorExternalId = "0192:986252932";

        // Act - Insert multiple statuses in sequence
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, actorExternalId, cancellationToken: default);
        await Task.Delay(50); // Small delay to ensure different timestamps
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadStarted, actorExternalId, cancellationToken: default);
        await Task.Delay(50);
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadConfirmed, actorExternalId, cancellationToken: default);

        // Assert - Should have the latest status
        await using var command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((int)ActorFileTransferStatus.DownloadConfirmed, reader.GetInt32(reader.GetOrdinal("latest_actor_status_id")));
    }

    [Fact]
    public async Task InsertActorFileTransferStatus_DifferentActors_HandledSeparately()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var actor1ExternalId = "0192:986252932";
        var actor2ExternalId = "0192:999888777";

        // Act - Insert statuses for different actors
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, actor1ExternalId, cancellationToken: default);
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadStarted, actor2ExternalId, cancellationToken: default);
        await Task.Delay(50);
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadConfirmed, actor1ExternalId, cancellationToken: default);

        // Assert - Each actor should have their own latest status
        await using var actor1Command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        actor1Command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        actor1Command.Parameters.AddWithValue("@actorExternalId", actor1ExternalId);
        
        await using var actor1Reader = await actor1Command.ExecuteReaderAsync();
        Assert.True(await actor1Reader.ReadAsync());
        Assert.Equal((int)ActorFileTransferStatus.DownloadConfirmed, actor1Reader.GetInt32(actor1Reader.GetOrdinal("latest_actor_status_id")));
        await actor1Reader.CloseAsync();

        await using var actor2Command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        actor2Command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        actor2Command.Parameters.AddWithValue("@actorExternalId", actor2ExternalId);
        
        await using var actor2Reader = await actor2Command.ExecuteReaderAsync();
        Assert.True(await actor2Reader.ReadAsync());
        Assert.Equal((int)ActorFileTransferStatus.DownloadStarted, actor2Reader.GetInt32(actor2Reader.GetOrdinal("latest_actor_status_id")));
    }

    [Fact]
    public async Task InsertActorFileTransferStatus_RepeatedInsert_UpdatesToLatest()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var actorExternalId = "0192:986252932";
        
        // Insert first status via repository
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, actorExternalId, cancellationToken: default);
        
        // Wait to ensure different timestamp
        await Task.Delay(100);
        
        // Insert second status via repository (will have newer timestamp and higher ID)
        await _repository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.DownloadConfirmed, actorExternalId, cancellationToken: default);

        // Assert - Should have the latest status (from the second insert)
        await using var command = _dataSource.CreateCommand(
            @"SELECT latest_actor_status_id 
              FROM broker.actor_file_transfer_latest_status afls
              INNER JOIN broker.actor a ON a.actor_id_pk = afls.actor_id_fk
              WHERE afls.file_transfer_id_fk = @fileTransferId AND a.actor_external_id = @actorExternalId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((int)ActorFileTransferStatus.DownloadConfirmed, reader.GetInt32(reader.GetOrdinal("latest_actor_status_id")));
    }

    private async Task<Guid> CreateFileTransferInDatabase()
    {
        var fileTransferId = Guid.NewGuid();
        var resourceId = TestConstants.RESOURCE_FOR_TEST;
        var senderExternalId = "0192:991825827";
        
        // Get or create actor
        await using var getActorCommand = _dataSource.CreateCommand(
            "SELECT actor_id_pk FROM broker.actor WHERE actor_external_id = @externalId");
        getActorCommand.Parameters.AddWithValue("@externalId", senderExternalId);
        var actorIdObj = await getActorCommand.ExecuteScalarAsync();
        
        long actorId;
        if (actorIdObj == null)
        {
            await using var insertActorCommand = _dataSource.CreateCommand(
                "INSERT INTO broker.actor (actor_external_id) VALUES (@externalId) RETURNING actor_id_pk");
            insertActorCommand.Parameters.AddWithValue("@externalId", senderExternalId);
            actorId = (long)(await insertActorCommand.ExecuteScalarAsync())!;
        }
        else
        {
            actorId = (long)actorIdObj;
        }

        // Get storage provider (assuming one exists from test setup)
        await using var getStorageCommand = _dataSource.CreateCommand(
            "SELECT storage_provider_id_pk FROM broker.storage_provider LIMIT 1");
        var storageProviderIdObj = await getStorageCommand.ExecuteScalarAsync();
        
        long storageProviderId;
        if (storageProviderIdObj == null)
        {
            // Create a service owner and storage provider if they don't exist
            await using var insertServiceOwnerCommand = _dataSource.CreateCommand(
                "INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name) VALUES (@id, @name) ON CONFLICT DO NOTHING");
            insertServiceOwnerCommand.Parameters.AddWithValue("@id", "0192:991825827");
            insertServiceOwnerCommand.Parameters.AddWithValue("@name", "Test Service Owner");
            await insertServiceOwnerCommand.ExecuteNonQueryAsync();

            await using var insertStorageCommand = _dataSource.CreateCommand(
                "INSERT INTO broker.storage_provider (service_owner_id_fk, created, storage_provider_type, resource_name, active) VALUES (@serviceOwnerId, NOW(), @type, @resourceName, true) RETURNING storage_provider_id_pk");
            insertStorageCommand.Parameters.AddWithValue("@serviceOwnerId", "0192:991825827");
            insertStorageCommand.Parameters.AddWithValue("@type", "Azurite");
            insertStorageCommand.Parameters.AddWithValue("@resourceName", "test-storage");
            storageProviderId = (long)(await insertStorageCommand.ExecuteScalarAsync())!;
        }
        else
        {
            storageProviderId = (long)storageProviderIdObj;
        }

        // Create file transfer
        await using var insertFileTransferCommand = _dataSource.CreateCommand(
            "INSERT INTO broker.file_transfer (file_transfer_id_pk, resource_id, filename, checksum, file_transfer_size, external_file_transfer_reference, sender_actor_id_fk, created, storage_provider_id_fk, expiration_time, hangfire_job_id, use_virus_scan) " +
            "VALUES (@fileTransferId, @resourceId, @fileName, NULL, NULL, @externalRef, @actorId, NOW(), @storageProviderId, @expirationTime, NULL, false)");
        insertFileTransferCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        insertFileTransferCommand.Parameters.AddWithValue("@resourceId", resourceId);
        insertFileTransferCommand.Parameters.AddWithValue("@fileName", "test.txt");
        insertFileTransferCommand.Parameters.AddWithValue("@externalRef", "test-ref");
        insertFileTransferCommand.Parameters.AddWithValue("@actorId", actorId);
        insertFileTransferCommand.Parameters.AddWithValue("@storageProviderId", storageProviderId);
        insertFileTransferCommand.Parameters.AddWithValue("@expirationTime", DateTime.UtcNow.AddHours(1));
        
        await insertFileTransferCommand.ExecuteNonQueryAsync();

        return fileTransferId;
    }

}

