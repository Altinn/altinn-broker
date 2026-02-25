using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class FileTransferStatusRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IFileTransferStatusRepository _repository;
    private readonly NpgsqlDataSource _dataSource;

    public FileTransferStatusRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _repository = factory.Services.GetRequiredService<IFileTransferStatusRepository>();
        _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
    }

    [Fact]
    public async Task InsertFileTransferStatus_FirstStatus_UpdatesDenormalizedColumns()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: default);

        // Assert
        await using var command = _dataSource.CreateCommand(
            "SELECT latest_file_status_id, latest_file_status_date FROM broker.file_transfer WHERE file_transfer_id_pk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((int)FileTransferStatus.Initialized, reader.GetInt32(reader.GetOrdinal("latest_file_status_id")));
        var dateOrdinal = reader.GetOrdinal("latest_file_status_date");
        Assert.False(reader.IsDBNull(dateOrdinal));
    }

    [Fact]
    public async Task InsertFileTransferStatus_WithDetailedStatus_StoresCorrectly()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var detailedStatus = "Custom detailed status message";

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Failed, detailedStatus, cancellationToken: default);

        // Assert - Check both the status record and denormalized columns
        await using var statusCommand = _dataSource.CreateCommand(
            "SELECT file_transfer_status_detailed_description FROM broker.file_transfer_status WHERE file_transfer_id_fk = @fileTransferId ORDER BY file_transfer_status_id_pk DESC LIMIT 1");
        statusCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        
        await using var statusReader = await statusCommand.ExecuteReaderAsync();
        Assert.True(await statusReader.ReadAsync());
        Assert.Equal(detailedStatus, statusReader.GetString(statusReader.GetOrdinal("file_transfer_status_detailed_description")));
        
        // Verify denormalized column was also updated
        await using var denormCommand = _dataSource.CreateCommand(
            "SELECT latest_file_status_id FROM broker.file_transfer WHERE file_transfer_id_pk = @fileTransferId");
        denormCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        
        await using var denormReader = await denormCommand.ExecuteReaderAsync();
        Assert.True(await denormReader.ReadAsync());
        Assert.Equal((int)FileTransferStatus.Failed, denormReader.GetInt32(denormReader.GetOrdinal("latest_file_status_id")));
    }

    [Fact]
    public async Task InsertFileTransferStatus_MultipleStatuses_KeepsLatest()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();

        // Act - Insert multiple statuses in sequence
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: default);
        await Task.Delay(50); // Small delay to ensure different timestamps
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, cancellationToken: default);
        await Task.Delay(50);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: default);
        await Task.Delay(50);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Published, cancellationToken: default);

        // Assert - Should have the latest status
        await using var command = _dataSource.CreateCommand(
            "SELECT latest_file_status_id FROM broker.file_transfer WHERE file_transfer_id_pk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((int)FileTransferStatus.Published, reader.GetInt32(reader.GetOrdinal("latest_file_status_id")));
    }

    [Fact]
    public async Task InsertFileTransferStatus_WithExplicitTimestamp_StoresGivenTimestamp()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var explicitTimestamp = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, timestamp: explicitTimestamp, cancellationToken: default);

        // Assert
        var (statusId, statusDate) = await GetLatestDenormalizedStatus(fileTransferId);
        Assert.Equal((int)FileTransferStatus.UploadStarted, statusId);
        Assert.Equal(explicitTimestamp, statusDate);
    }

    [Fact]
    public async Task InsertFileTransferStatus_WhenOlderStatusInsertedLater_DoesNotOverwriteDenormalizedLatest()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var newerTimestamp = new DateTimeOffset(2026, 01, 01, 12, 00, 10, TimeSpan.Zero);
        var olderTimestamp = newerTimestamp.AddSeconds(-10);

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadProcessing, timestamp: newerTimestamp, cancellationToken: default);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, timestamp: olderTimestamp, cancellationToken: default);

        // Assert
        var (statusId, statusDate) = await GetLatestDenormalizedStatus(fileTransferId);
        Assert.Equal((int)FileTransferStatus.UploadProcessing, statusId);
        Assert.Equal(newerTimestamp, statusDate);
    }

    [Fact]
    public async Task InsertFileTransferStatus_WhenSameTimestamp_UsesStatusIdAsTieBreaker()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var sharedTimestamp = new DateTimeOffset(2026, 01, 01, 12, 30, 00, TimeSpan.Zero);

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, timestamp: sharedTimestamp, cancellationToken: default);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadProcessing, timestamp: sharedTimestamp, cancellationToken: default);

        // Assert
        var (statusIdAfterHigher, statusDateAfterHigher) = await GetLatestDenormalizedStatus(fileTransferId);
        Assert.Equal((int)FileTransferStatus.UploadProcessing, statusIdAfterHigher);
        Assert.Equal(sharedTimestamp, statusDateAfterHigher);

        // Act - lower status with same timestamp should not overwrite
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, timestamp: sharedTimestamp, cancellationToken: default);

        // Assert
        var (statusIdAfterLower, statusDateAfterLower) = await GetLatestDenormalizedStatus(fileTransferId);
        Assert.Equal((int)FileTransferStatus.UploadProcessing, statusIdAfterLower);
        Assert.Equal(sharedTimestamp, statusDateAfterLower);
    }

    private async Task<(int LatestStatusId, DateTimeOffset LatestStatusDate)> GetLatestDenormalizedStatus(Guid fileTransferId)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT latest_file_status_id, latest_file_status_date FROM broker.file_transfer WHERE file_transfer_id_pk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var latestStatusDate = reader.GetDateTime(reader.GetOrdinal("latest_file_status_date"));
        var latestStatusDateUtc = DateTime.SpecifyKind(latestStatusDate, DateTimeKind.Utc);

        return (
            reader.GetInt32(reader.GetOrdinal("latest_file_status_id")),
            new DateTimeOffset(latestStatusDateUtc)
        );
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

