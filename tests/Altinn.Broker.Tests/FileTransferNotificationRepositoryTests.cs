using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class FileTransferNotificationRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly IFileTransferNotificationRepository _repository;
    private readonly NpgsqlDataSource _dataSource;
    private readonly TestDataHelper _dataHelper;

    public FileTransferNotificationRepositoryTests(CustomWebApplicationFactory factory)
    {
        _repository = factory.Services.GetRequiredService<IFileTransferNotificationRepository>();
        _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        _dataHelper = new TestDataHelper(_dataSource);
    }

    private const string RelatedOrganization = "0192:987654321";

    /// <summary>
    /// <paramref name="customRecipientRelatedOrganization"/> is only set when <paramref name="customRecipientType"/> is.
    /// </summary>
    private static BrokerNotificationEntity CreateEntity(
        Guid fileTransferId,
        long? actorId = null,
        CustomRecipientType? customRecipientType = null,
        string? customRecipientIdentifier = null,
        string? customRecipientRelatedOrganization = RelatedOrganization,
        bool isReminder = false) => new()
    {
        Id = Guid.NewGuid(),
        FileTransferId = fileTransferId,
        ActorId = actorId,
        CustomRecipientType = customRecipientType,
        CustomRecipientIdentifier = customRecipientIdentifier,
        CustomRecipientRelatedOrganization = customRecipientType is null ? null : customRecipientRelatedOrganization,
        NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
        NotificationChannel = NotificationChannel.Email,
        RequestedSendTime = DateTimeOffset.UtcNow,
        Created = DateTimeOffset.UtcNow,
        IsReminder = isReminder,
        OrderRequest = "{}"
    };

    [Fact]
    public async Task AddNotification_WithActorRecipient_RoundTripsAllFields()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var actor = await _dataHelper.GetOrCreateActor("0192:123456789");
        var entity = CreateEntity(fileTransferId, actorId: actor.ActorId);

        await _repository.AddNotification(entity, CancellationToken.None);

        var notifications = await _repository.GetNotificationsForFileTransfer(fileTransferId, CancellationToken.None);
        var stored = Assert.Single(notifications);
        Assert.Equal(entity.Id, stored.Id);
        Assert.Equal(fileTransferId, stored.FileTransferId);
        Assert.Equal(actor.ActorId, stored.ActorId);
        Assert.Null(stored.CustomRecipientType);
        Assert.Null(stored.CustomRecipientIdentifier);
        Assert.Null(stored.CustomRecipientRelatedOrganization);
        Assert.Equal(NotificationTemplate.GenericAltinnMessage, stored.NotificationTemplate);
        Assert.Equal(NotificationChannel.Email, stored.NotificationChannel);
        Assert.False(stored.IsReminder);
        Assert.Null(stored.NotificationSent);
        Assert.Null(stored.NotificationOrderId);
        Assert.Null(stored.ShipmentId);
    }

    [Theory]
    [InlineData(CustomRecipientType.Organization, "0192:123456789")]
    [InlineData(CustomRecipientType.Person, "urn:altinn:person:identifier-no:01819012012")]
    [InlineData(CustomRecipientType.Email, "test@example.com")]
    [InlineData(CustomRecipientType.MobileNumber, "+4799999999")]
    public async Task AddNotification_WithCustomRecipient_RoundTripsTypeAndIdentifierWithoutActorId(CustomRecipientType type, string identifier)
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var entity = CreateEntity(fileTransferId, customRecipientType: type, customRecipientIdentifier: identifier);

        await _repository.AddNotification(entity, CancellationToken.None);

        var notifications = await _repository.GetNotificationsForFileTransfer(fileTransferId, CancellationToken.None);
        var stored = Assert.Single(notifications);
        Assert.Null(stored.ActorId);
        Assert.Equal(type, stored.CustomRecipientType);
        Assert.Equal(identifier, stored.CustomRecipientIdentifier);
        Assert.Equal(RelatedOrganization, stored.CustomRecipientRelatedOrganization);
    }

    [Fact]
    public async Task AddNotification_WithCustomRecipientWithoutRelatedOrganization_IsRejectedByDatabase()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var entity = CreateEntity(fileTransferId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com", customRecipientRelatedOrganization: null);

        await Assert.ThrowsAsync<PostgresException>(() => _repository.AddNotification(entity, CancellationToken.None));
    }

    [Fact]
    public async Task AddNotification_WithSameCustomRecipientForTwoRelatedOrganizations_StoresBoth()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);

        await _repository.AddNotification(CreateEntity(fileTransferId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com", customRecipientRelatedOrganization: "0192:111111111"), CancellationToken.None);
        await _repository.AddNotification(CreateEntity(fileTransferId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com", customRecipientRelatedOrganization: "0192:222222222"), CancellationToken.None);

        var notifications = await _repository.GetNotificationsForFileTransfer(fileTransferId, CancellationToken.None);
        Assert.Equal(2, notifications.Count);
    }

    [Fact]
    public async Task AddNotification_WithBothActorAndCustomRecipient_IsRejectedByDatabase()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var actor = await _dataHelper.GetOrCreateActor("0192:123456789");
        var entity = CreateEntity(fileTransferId, actorId: actor.ActorId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com");

        await Assert.ThrowsAsync<PostgresException>(() => _repository.AddNotification(entity, CancellationToken.None));
    }

    [Fact]
    public async Task GetNotificationById_ReturnsMatchingRow_AndNullForUnknownId()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var entity = CreateEntity(fileTransferId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com");
        await _repository.AddNotification(entity, CancellationToken.None);

        var found = await _repository.GetNotificationById(entity.Id, CancellationToken.None);
        var notFound = await _repository.GetNotificationById(Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(entity.Id, found!.Id);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task UpdateOrderResponseData_SetsOrderAndShipmentId_ButNotNotificationSent()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var entity = CreateEntity(fileTransferId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com");
        await _repository.AddNotification(entity, CancellationToken.None);
        var notificationOrderId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        await _repository.UpdateOrderResponseData(entity.Id, notificationOrderId, shipmentId, CancellationToken.None);

        var updated = await _repository.GetNotificationById(entity.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(notificationOrderId, updated!.NotificationOrderId);
        Assert.Equal(shipmentId, updated.ShipmentId);
        Assert.Null(updated.NotificationSent);
    }

    [Fact]
    public async Task UpdateNotificationSent_SetsSentTimeAndAddress()
    {
        var fileTransferId = await _dataHelper.InsertFileTransfer(TestConstants.RESOURCE_FOR_TEST);
        var entity = CreateEntity(fileTransferId, customRecipientType: CustomRecipientType.Email, customRecipientIdentifier: "test@example.com");
        await _repository.AddNotification(entity, CancellationToken.None);
        var sentTime = DateTimeOffset.UtcNow.AddMinutes(-3);

        await _repository.UpdateNotificationSent(entity.Id, sentTime, "test@example.com, +4712345678", CancellationToken.None);

        var updated = await _repository.GetNotificationById(entity.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("test@example.com, +4712345678", updated!.NotificationAddress);
        AssertCloseTo(sentTime, updated.NotificationSent!.Value);
    }

    private static void AssertCloseTo(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.True(
            Math.Abs((expected - actual).TotalSeconds) < 1,
            $"Expected {expected:o} to be close to {actual:o}");
    }
}
