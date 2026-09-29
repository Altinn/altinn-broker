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
    private readonly TestDataHelper _dataHelper;

    public FileTransferStatusRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _repository = factory.Services.GetRequiredService<IFileTransferStatusRepository>();
        _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        _dataHelper = new TestDataHelper(_dataSource);
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
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, cancellationToken: default);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: default);
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
    public async Task InsertFileTransferStatus_SetsTimestampsFromDatabaseInInsertionOrder()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: default);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, cancellationToken: default);
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: default);

        // Assert
        await using var command = _dataSource.CreateCommand(
            "SELECT file_transfer_status_date FROM broker.file_transfer_status WHERE file_transfer_id_fk = @fileTransferId ORDER BY file_transfer_status_id_pk");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        var statusDates = new List<DateTime>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                statusDates.Add(reader.GetDateTime(0));
            }
        }
        Assert.Equal(3, statusDates.Count);
        Assert.True(statusDates[0] < statusDates[1] && statusDates[1] < statusDates[2]);

        var (statusId, statusDate) = await GetLatestDenormalizedStatus(fileTransferId);
        Assert.Equal((int)FileTransferStatus.UploadProcessing, statusId);
        Assert.Equal(statusDates[2], statusDate.UtcDateTime);
    }

    [Fact]
    public async Task InsertFileTransferStatus_WhenStoredLatestIsNewer_DoesNotOverwriteDenormalizedLatest()
    {
        // Arrange
        var fileTransferId = await CreateFileTransferInDatabase();
        var futureTimestamp = new DateTimeOffset(2100, 01, 01, 12, 00, 00, TimeSpan.Zero);
        await _dataHelper.SetLatestFileTransferStatus(fileTransferId, FileTransferStatus.UploadProcessing, futureTimestamp);

        // Act
        await _repository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.UploadStarted, cancellationToken: default);

        // Assert
        var (statusId, statusDate) = await GetLatestDenormalizedStatus(fileTransferId);
        Assert.Equal((int)FileTransferStatus.UploadProcessing, statusId);
        Assert.Equal(futureTimestamp, statusDate);
    }

    [Fact]
    public async Task GetInitializedFileTransfersWithStartedUploadOlderThanDate_ReturnsOnlyTransfersStuckInInitialized()
    {
        // Arrange
        var stuckFileTransferId = await CreateFileTransferInDatabase();
        await _repository.InsertFileTransferStatus(stuckFileTransferId, FileTransferStatus.Initialized, cancellationToken: default);
        // Inserted without updating the denormalized columns, leaving the current status at Initialized
        await _dataHelper.InsertFileTransferStatus(stuckFileTransferId, FileTransferStatus.UploadStarted, DateTimeOffset.UtcNow);

        var notUploadedFileTransferId = await CreateFileTransferInDatabase();
        await _repository.InsertFileTransferStatus(notUploadedFileTransferId, FileTransferStatus.Initialized, cancellationToken: default);

        var uploadingFileTransferId = await CreateFileTransferInDatabase();
        await _repository.InsertFileTransferStatus(uploadingFileTransferId, FileTransferStatus.Initialized, cancellationToken: default);
        await _repository.InsertFileTransferStatus(uploadingFileTransferId, FileTransferStatus.UploadStarted, cancellationToken: default);

        // Act
        var result = await _repository.GetInitializedFileTransfersWithStartedUploadOlderThanDate(DateTime.UtcNow.AddHours(1), default);

        // Assert
        var fileTransferIds = result.Select(status => status.FileTransferId).ToList();
        Assert.Contains(stuckFileTransferId, fileTransferIds);
        Assert.DoesNotContain(notUploadedFileTransferId, fileTransferIds);
        Assert.DoesNotContain(uploadingFileTransferId, fileTransferIds);
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
        return await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
    }
}

