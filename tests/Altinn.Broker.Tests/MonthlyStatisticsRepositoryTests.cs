using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class MonthlyStatisticsRepositoryTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly IMonthlyStatisticsRepository _repository;
    private readonly TestDataHelper _dataHelper;
    private readonly HashSet<string> _createdResourceIds = [];
    private readonly HashSet<string> _createdServiceOwnerIds = [];

    public MonthlyStatisticsRepositoryTests(CustomWebApplicationFactory factory)
    {
        _repository = factory.Services.GetRequiredService<IMonthlyStatisticsRepository>();
        _dataHelper = new TestDataHelper(factory.Services.GetRequiredService<NpgsqlDataSource>());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _dataHelper.DeleteTestData(_createdResourceIds, _createdServiceOwnerIds);
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_AggregatesBySenderRecipientPairForSelectedMonth()
    {
        var serviceOwnerOrganizationNumber = CreateUniqueOrganizationNumber();
        var otherServiceOwnerOrganizationNumber = CreateUniqueOrganizationNumber();
        var serviceOwnerId = $"0192:{serviceOwnerOrganizationNumber}";
        var otherServiceOwnerId = $"0192:{otherServiceOwnerOrganizationNumber}";
        var resourceA = $"monthly-stats-a-{Guid.NewGuid():N}";
        var resourceB = $"monthly-stats-b-{Guid.NewGuid():N}";
        var otherResource = $"monthly-stats-other-{Guid.NewGuid():N}";

        RegisterCreatedTestData(serviceOwnerId, resourceA);
        RegisterCreatedTestData(serviceOwnerId, resourceB);
        RegisterCreatedTestData(otherServiceOwnerId, otherResource);

        await _dataHelper.EnsureResource(resourceA, serviceOwnerOrganizationNumber, serviceOwnerId);
        await _dataHelper.EnsureResource(resourceB, serviceOwnerOrganizationNumber, serviceOwnerId);
        await _dataHelper.EnsureResource(otherResource, otherServiceOwnerOrganizationNumber, otherServiceOwnerId);

        var senderA = "0192:991825827";
        var senderB = "0192:312195771";
        var transferA1 = await _dataHelper.InsertFileTransfer(resourceA, serviceOwnerId, created: new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderA);
        var transferA2 = await _dataHelper.InsertFileTransfer(resourceA, serviceOwnerId, created: new DateTimeOffset(2026, 1, 20, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderA);
        var transferA3InitializedOnly = await _dataHelper.InsertFileTransfer(resourceA, serviceOwnerId, created: new DateTimeOffset(2026, 1, 22, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderA);
        var transferB1 = await _dataHelper.InsertFileTransfer(resourceB, serviceOwnerId, created: new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderB);
        var transferOther = await _dataHelper.InsertFileTransfer(otherResource, otherServiceOwnerId, created: new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero));

        await _dataHelper.InsertActorStatus(transferA1, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA1, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA2, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 20, 11, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA3InitializedOnly, "0192:986252932", ActorFileTransferStatus.Initialized, new DateTimeOffset(2026, 1, 22, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA1, "0192:986252933", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 12, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferB1, "0192:986252935", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 13, 8, 30, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferOther, "0192:986252936", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 5, 8, 30, 0, TimeSpan.Zero));

        await _dataHelper.InsertFileTransferStatus(transferA1, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(transferA2, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(transferB1, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(transferOther, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));

        await _dataHelper.InsertActorStatus(transferA1, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 11, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA1, "0192:986252933", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA2, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 25, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferB1, "0192:986252935", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 13, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferOther, "0192:986252936", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 6, 9, 0, 0, TimeSpan.Zero));

        await RefreshMonthlyStatisticsRollupAsync();

        var rows = await _repository.GetMonthlyResourceStatisticsData(
            serviceOwnerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            CancellationToken.None);

        Assert.Equal(3, rows.Count);

        var pairRowA1 = Assert.Single(rows, row => row.ResourceId == resourceA && row.Sender == senderA && row.Recipient == "0192:986252932");
        Assert.Equal(3, pairRowA1.TotalFileTransfers);
        Assert.Equal(2, pairRowA1.UploadCount);
        Assert.Equal(3, pairRowA1.DownloadStartedCount);
        Assert.Equal(2, pairRowA1.UniqueDownloadStartedCount);
        Assert.Equal(2, pairRowA1.DownloadConfirmedCount);

        var pairRowA2 = Assert.Single(rows, row => row.ResourceId == resourceA && row.Sender == senderA && row.Recipient == "0192:986252933");
        Assert.Equal(1, pairRowA2.TotalFileTransfers);
        Assert.Equal(1, pairRowA2.UploadCount);
        Assert.Equal(1, pairRowA2.DownloadStartedCount);
        Assert.Equal(1, pairRowA2.UniqueDownloadStartedCount);
        Assert.Equal(0, pairRowA2.DownloadConfirmedCount);

        var pairRowB = Assert.Single(rows, row => row.ResourceId == resourceB && row.Sender == senderB && row.Recipient == "0192:986252935");
        Assert.Equal(1, pairRowB.TotalFileTransfers);
        Assert.Equal(1, pairRowB.UploadCount);
        Assert.Equal(1, pairRowB.DownloadStartedCount);
        Assert.Equal(1, pairRowB.UniqueDownloadStartedCount);
        Assert.Equal(1, pairRowB.DownloadConfirmedCount);
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_ResourceFilterRestrictsRows()
    {
        var serviceOwnerOrganizationNumber = CreateUniqueOrganizationNumber();
        var serviceOwnerId = $"0192:{serviceOwnerOrganizationNumber}";
        var resourceA = $"monthly-filter-a-{Guid.NewGuid():N}";
        var resourceB = $"monthly-filter-b-{Guid.NewGuid():N}";

        RegisterCreatedTestData(serviceOwnerId, resourceA);
        RegisterCreatedTestData(serviceOwnerId, resourceB);

        await _dataHelper.EnsureResource(resourceA, serviceOwnerOrganizationNumber, serviceOwnerId);
        await _dataHelper.EnsureResource(resourceB, serviceOwnerOrganizationNumber, serviceOwnerId);

        var transferA = await _dataHelper.InsertFileTransfer(resourceA, serviceOwnerId, created: new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderExternalId: "0192:991825827");
        var transferB = await _dataHelper.InsertFileTransfer(resourceB, serviceOwnerId, created: new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero), senderExternalId: "0192:312195771");

        await _dataHelper.InsertFileTransferStatus(transferA, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(transferB, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 11, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferB, "0192:986252933", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferB, "0192:986252933", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));

        await RefreshMonthlyStatisticsRollupAsync();

        var rows = await _repository.GetMonthlyResourceStatisticsData(
            serviceOwnerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            resourceA,
            null,
            CancellationToken.None);

        Assert.Single(rows);
        Assert.All(rows, row => Assert.Equal(resourceA, row.ResourceId));
        Assert.Contains(rows, row => row.Sender == "0192:991825827" && row.Recipient == "0192:986252932" && row.TotalFileTransfers == 1 && row.UploadCount == 1 && row.DownloadStartedCount == 1 && row.UniqueDownloadStartedCount == 1 && row.DownloadConfirmedCount == 1);
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_GroupBySelectedProperties_SplitsRowsByPropertyValues()
    {
        var serviceOwnerOrganizationNumber = CreateUniqueOrganizationNumber();
        var serviceOwnerId = $"0192:{serviceOwnerOrganizationNumber}";
        var resourceId = $"monthly-grouping-{Guid.NewGuid():N}";
        var senderId = "0192:991825827";
        var recipientId = "0192:986252932";

        RegisterCreatedTestData(serviceOwnerId, resourceId);

        await _dataHelper.EnsureResource(resourceId, serviceOwnerOrganizationNumber, serviceOwnerId);

        var transferA = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);
        var transferB = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);

        await _dataHelper.InsertProperty(transferA, "messageType", "invoice");
        await _dataHelper.InsertProperty(transferA, "statusMessage", "accepted");
        await _dataHelper.InsertProperty(transferB, "messageType", "receipt");

        await _dataHelper.InsertFileTransferStatus(transferA, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferB, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 11, 10, 0, 0, TimeSpan.Zero));

        await RefreshMonthlyStatisticsRollupAsync();

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
        Assert.Equal(1, invoiceRow.TotalFileTransfers);
        Assert.Equal(1, invoiceRow.DownloadStartedCount);
        Assert.Equal(1, invoiceRow.UniqueDownloadStartedCount);
        Assert.Equal(1, invoiceRow.DownloadConfirmedCount);

        var receiptRow = Assert.Single(rows, row => row.GroupedPropertyValues.TryGetValue("messageType", out var value) && value == "receipt");
        Assert.False(receiptRow.GroupedPropertyValues.ContainsKey("statusMessage"));
        Assert.Equal(1, receiptRow.TotalFileTransfers);
        Assert.Equal(0, receiptRow.UploadCount);
        Assert.Equal(1, receiptRow.DownloadStartedCount);
        Assert.Equal(1, receiptRow.UniqueDownloadStartedCount);
        Assert.Equal(0, receiptRow.DownloadConfirmedCount);
    }

    [Fact]
    public async Task GetMonthlyResourceStatisticsData_GroupBySubsetOfProperties_SumsAcrossUnselectedProperties()
    {
        var serviceOwnerOrganizationNumber = CreateUniqueOrganizationNumber();
        var serviceOwnerId = $"0192:{serviceOwnerOrganizationNumber}";
        var resourceId = $"monthly-grouping-subset-{Guid.NewGuid():N}";
        var senderId = "0192:991825827";
        var recipientId = "0192:986252932";

        RegisterCreatedTestData(serviceOwnerId, resourceId);

        await _dataHelper.EnsureResource(resourceId, serviceOwnerOrganizationNumber, serviceOwnerId);

        var transferA = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);
        var transferB = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);

        await _dataHelper.InsertProperty(transferA, "messageType", "invoice");
        await _dataHelper.InsertProperty(transferA, "statusMessage", "accepted");
        await _dataHelper.InsertProperty(transferB, "messageType", "invoice");
        await _dataHelper.InsertProperty(transferB, "statusMessage", "rejected");

        await _dataHelper.InsertFileTransferStatus(transferA, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(transferB, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 11, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(transferB, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 11, 10, 0, 0, TimeSpan.Zero));

        await RefreshMonthlyStatisticsRollupAsync();

        var rows = await _repository.GetMonthlyResourceStatisticsData(
            serviceOwnerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            resourceId,
            ["messageType"],
            CancellationToken.None);

        var invoiceRow = Assert.Single(rows);
        Assert.Equal("invoice", invoiceRow.GroupedPropertyValues["messageType"]);
        Assert.Equal(2, invoiceRow.TotalFileTransfers);
        Assert.Equal(2, invoiceRow.UploadCount);
        Assert.Equal(2, invoiceRow.DownloadStartedCount);
        Assert.Equal(2, invoiceRow.UniqueDownloadStartedCount);
        Assert.Equal(0, invoiceRow.DownloadConfirmedCount);
    }

    private void RegisterCreatedTestData(string serviceOwnerId, string resourceId)
    {
        _createdServiceOwnerIds.Add(serviceOwnerId);
        _createdResourceIds.Add(resourceId);
    }

    private Task RefreshMonthlyStatisticsRollupAsync()
        => _repository.RefreshMonthlyStatisticsRollup(CancellationToken.None);

    private static string CreateUniqueOrganizationNumber()
    {
        var value = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) % 1_000_000_000;
        return value.ToString("D9");
    }
}