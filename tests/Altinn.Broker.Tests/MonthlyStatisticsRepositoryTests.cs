using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class MonthlyStatisticsRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly IFileTransferRepository _repository;
    private readonly NpgsqlDataSource _dataSource;

    public MonthlyStatisticsRepositoryTests(CustomWebApplicationFactory factory)
    {
        _repository = factory.Services.GetRequiredService<IFileTransferRepository>();
        _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_AggregatesBySenderRecipientPairForSelectedMonth()
    {
        var serviceOwnerId = "0192:991825827";
        var otherServiceOwnerId = "0192:313301753";
        var resourceA = $"monthly-stats-a-{Guid.NewGuid():N}";
        var resourceB = $"monthly-stats-b-{Guid.NewGuid():N}";
        var otherResource = $"monthly-stats-other-{Guid.NewGuid():N}";

        await EnsureResource(resourceA, "991825827", serviceOwnerId);
        await EnsureResource(resourceB, "991825827", serviceOwnerId);
        await EnsureResource(otherResource, "313301753", otherServiceOwnerId);

        var senderA = "0192:991825827";
        var senderB = "0192:312195771";
        var transferA1 = await InsertFileTransfer(resourceA, serviceOwnerId, new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderA);
        var transferA2 = await InsertFileTransfer(resourceA, serviceOwnerId, new DateTimeOffset(2026, 1, 20, 8, 0, 0, TimeSpan.Zero), senderA);
        var transferB1 = await InsertFileTransfer(resourceB, serviceOwnerId, new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero), senderB);
        var transferOther = await InsertFileTransfer(otherResource, otherServiceOwnerId, new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero));

        await InsertActorStatus(transferA1, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA1, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA2, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 20, 11, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA1, "0192:986252933", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 12, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferB1, "0192:986252935", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 13, 8, 30, 0, TimeSpan.Zero));
        await InsertActorStatus(transferOther, "0192:986252936", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 5, 8, 30, 0, TimeSpan.Zero));

        await InsertActorStatus(transferA1, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 11, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA1, "0192:986252933", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA2, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 25, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferB1, "0192:986252935", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 13, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferOther, "0192:986252936", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 6, 9, 0, 0, TimeSpan.Zero));

        var rows = await _repository.GetMonthlyResourceStatisticsData(
            serviceOwnerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            CancellationToken.None);

        Assert.Equal(3, rows.Count);

        var pairRowA1 = Assert.Single(rows, row => row.ResourceId == resourceA && row.Sender == senderA && row.Recipient == "0192:986252932");
        Assert.Equal(2, pairRowA1.UploadCount);
        Assert.Equal(3, pairRowA1.DownloadStartedCount);
        Assert.Equal(2, pairRowA1.UniqueDownloadStartedCount);
        Assert.Equal(2, pairRowA1.DownloadConfirmedCount);

        var pairRowA2 = Assert.Single(rows, row => row.ResourceId == resourceA && row.Sender == senderA && row.Recipient == "0192:986252933");
        Assert.Equal(1, pairRowA2.UploadCount);
        Assert.Equal(1, pairRowA2.DownloadStartedCount);
        Assert.Equal(1, pairRowA2.UniqueDownloadStartedCount);
        Assert.Equal(0, pairRowA2.DownloadConfirmedCount);

        var pairRowB = Assert.Single(rows, row => row.ResourceId == resourceB && row.Sender == senderB && row.Recipient == "0192:986252935");
        Assert.Equal(1, pairRowB.UploadCount);
        Assert.Equal(1, pairRowB.DownloadStartedCount);
        Assert.Equal(1, pairRowB.UniqueDownloadStartedCount);
        Assert.Equal(1, pairRowB.DownloadConfirmedCount);
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_ResourceFilterRestrictsRows()
    {
        var serviceOwnerId = "0192:991825827";
        var resourceA = $"monthly-filter-a-{Guid.NewGuid():N}";
        var resourceB = $"monthly-filter-b-{Guid.NewGuid():N}";

        await EnsureResource(resourceA, "991825827", serviceOwnerId);
        await EnsureResource(resourceB, "991825827", serviceOwnerId);

        var transferA = await InsertFileTransfer(resourceA, serviceOwnerId, new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), "0192:991825827");
        var transferB = await InsertFileTransfer(resourceB, serviceOwnerId, new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero), "0192:312195771");

        await InsertActorStatus(transferA, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferB, "0192:986252933", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferB, "0192:986252933", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));

        var rows = await _repository.GetMonthlyResourceStatisticsData(
            serviceOwnerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            resourceA,
            null,
            CancellationToken.None);

        Assert.Single(rows);
        Assert.All(rows, row => Assert.Equal(resourceA, row.ResourceId));
        Assert.Contains(rows, row => row.Sender == "0192:991825827" && row.Recipient == "0192:986252932" && row.UploadCount == 1 && row.DownloadStartedCount == 1 && row.UniqueDownloadStartedCount == 1 && row.DownloadConfirmedCount == 1);
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_GroupBySelectedProperties_SplitsRowsByPropertyValues()
    {
        var serviceOwnerId = "0192:991825827";
        var resourceId = $"monthly-grouping-{Guid.NewGuid():N}";
        var senderId = "0192:991825827";
        var recipientId = "0192:986252932";

        await EnsureResource(resourceId, "991825827", serviceOwnerId);

        var transferA = await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderId);
        var transferB = await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero), senderId);

        await InsertProperty(transferA, "messageType", "invoice");
        await InsertProperty(transferA, "statusMessage", "accepted");
        await InsertProperty(transferB, "messageType", "receipt");

        await InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferB, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 11, 10, 0, 0, TimeSpan.Zero));

        var rows = await _repository.GetMonthlyResourceStatisticsData(
            serviceOwnerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            resourceId,
            ["messageType", "statusMessage"],
            CancellationToken.None);

        Assert.Equal(2, rows.Count);

        var invoiceRow = Assert.Single(rows, row => row.GroupedPropertyValues.TryGetValue("messageType", out var value) && value == "invoice");
        Assert.Equal("accepted", invoiceRow.GroupedPropertyValues["statusMessage"]);
        Assert.Equal(1, invoiceRow.UploadCount);
        Assert.Equal(1, invoiceRow.DownloadStartedCount);
        Assert.Equal(1, invoiceRow.UniqueDownloadStartedCount);
        Assert.Equal(1, invoiceRow.DownloadConfirmedCount);

        var receiptRow = Assert.Single(rows, row => row.GroupedPropertyValues.TryGetValue("messageType", out var value) && value == "receipt");
        Assert.False(receiptRow.GroupedPropertyValues.ContainsKey("statusMessage"));
        Assert.Equal(1, receiptRow.UploadCount);
        Assert.Equal(1, receiptRow.DownloadStartedCount);
        Assert.Equal(1, receiptRow.UniqueDownloadStartedCount);
        Assert.Equal(0, receiptRow.DownloadConfirmedCount);
    }

    private async Task EnsureResource(string resourceId, string organizationNumber, string serviceOwnerId)
    {
        await EnsureStorageProvider(serviceOwnerId);

        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.altinn_resource (resource_id_pk, created, organization_number, service_owner_id_fk)
              VALUES (@resourceId, NOW(), @organizationNumber, @serviceOwnerId)
              ON CONFLICT (resource_id_pk) DO NOTHING");

        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertFileTransfer(string resourceId, string serviceOwnerId, DateTimeOffset created, string senderExternalId = "0192:991825827")
    {
        var fileTransferId = Guid.NewGuid();
        var senderActorId = await EnsureActor(senderExternalId);
        var storageProviderId = await EnsureStorageProvider(serviceOwnerId);

        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer (
                    file_transfer_id_pk, resource_id, created, filename, checksum, file_transfer_size,
                    sender_actor_id_fk, external_file_transfer_reference, expiration_time, storage_provider_id_fk, use_virus_scan)
              VALUES (
                    @fileTransferId, @resourceId, @created, @fileName, NULL, 0,
                    @senderActorId, @externalReference, @expirationTime, @storageProviderId, false)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@created", created.UtcDateTime);
        command.Parameters.AddWithValue("@fileName", "stats.txt");
        command.Parameters.AddWithValue("@senderActorId", senderActorId);
        command.Parameters.AddWithValue("@externalReference", $"ref-{fileTransferId}");
        command.Parameters.AddWithValue("@expirationTime", created.UtcDateTime.AddDays(1));
        command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
        await command.ExecuteNonQueryAsync();

        return fileTransferId;
    }

    private async Task InsertActorStatus(Guid fileTransferId, string actorExternalId, ActorFileTransferStatus status, DateTimeOffset statusDate)
    {
        var actorId = await EnsureActor(actorExternalId);

        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.actor_file_transfer_status (
                    actor_id_fk, file_transfer_id_fk, actor_file_transfer_status_description_id_fk, actor_file_transfer_status_date)
              VALUES (@actorId, @fileTransferId, @status, @statusDate)");

        command.Parameters.AddWithValue("@actorId", actorId);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@status", (int)status);
        command.Parameters.AddWithValue("@statusDate", statusDate.UtcDateTime);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertProperty(Guid fileTransferId, string key, string value)
    {
        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer_property (file_transfer_id_fk, key, value)
              VALUES (@fileTransferId, @key, @value)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> EnsureActor(string actorExternalId)
    {
        await using var selectCommand = _dataSource.CreateCommand(
            "SELECT actor_id_pk FROM broker.actor WHERE actor_external_id = @actorExternalId");
        selectCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        var existingActorId = await selectCommand.ExecuteScalarAsync();
        if (existingActorId is long actorId)
        {
            return actorId;
        }

        await using var insertCommand = _dataSource.CreateCommand(
            "INSERT INTO broker.actor (actor_external_id) VALUES (@actorExternalId) RETURNING actor_id_pk");
        insertCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        return (long)(await insertCommand.ExecuteScalarAsync())!;
    }

    private async Task<long> EnsureStorageProvider(string serviceOwnerId)
    {
        await using var selectCommand = _dataSource.CreateCommand(
            "SELECT storage_provider_id_pk FROM broker.storage_provider WHERE service_owner_id_fk = @serviceOwnerId LIMIT 1");
        selectCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        var existingStorageProviderId = await selectCommand.ExecuteScalarAsync();
        if (existingStorageProviderId is long storageProviderId)
        {
            return storageProviderId;
        }

        await using var insertServiceOwnerCommand = _dataSource.CreateCommand(
            @"INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name)
              VALUES (@serviceOwnerId, @serviceOwnerName)
              ON CONFLICT DO NOTHING");
        insertServiceOwnerCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        insertServiceOwnerCommand.Parameters.AddWithValue("@serviceOwnerName", $"Service owner {serviceOwnerId}");
        await insertServiceOwnerCommand.ExecuteNonQueryAsync();

        await using var insertStorageProviderCommand = _dataSource.CreateCommand(
            @"INSERT INTO broker.storage_provider (
                    service_owner_id_fk, created, storage_provider_type, resource_name, active)
              VALUES (@serviceOwnerId, NOW(), @storageProviderType, @resourceName, true)
              RETURNING storage_provider_id_pk");
        insertStorageProviderCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        insertStorageProviderCommand.Parameters.AddWithValue("@storageProviderType", "Azurite");
        insertStorageProviderCommand.Parameters.AddWithValue("@resourceName", $"stats-storage-{Guid.NewGuid():N}");

        return (long)(await insertStorageProviderCommand.ExecuteScalarAsync())!;
    }
}
