using System.Text.Json;

using Altinn.Broker.Application.CreateNotificationOrder;
using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class CreateNotificationOrderHandlerTests
{
    private const string DefaultRelatedOrganization = "111111111";
    private const string ValidNationalIdentityNumber = "01819012012";

    private static CreateNotificationOrderRequest CreateRequest(
        List<string>? recipientExternalIds = null,
        List<Recipient>? customRecipients = null,
        bool sendReminder = false,
        NotificationChannel channel = NotificationChannel.Email)
    {
        customRecipients?.ForEach(recipient => recipient.RelatedOrganizationNumber ??= DefaultRelatedOrganization);
        return new()
        {
            FileTransferId = Guid.NewGuid(),
            ResourceId = "resource123",
            SenderExternalId = "0192:991825827",
            FileName = "document.pdf",
            RecipientExternalIds = recipientExternalIds ?? [],
            FileTransferExpirationTime = DateTime.UtcNow.AddDays(30),
            NotificationRequest = new NotificationRequest
            {
                NotificationChannel = channel,
                SendReminder = sendReminder,
                CustomRecipients = customRecipients
            }
        };
    }

    private static NotificationTemplateEntity DefaultTemplate() => new()
    {
        Id = 0,
        NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
        Language = "nb",
        EmailSubject = "Message for $fileTransferRecipient$ from $sendersName$",
        EmailBody = "$fileTransferRecipient$ received $fileName$ (resource: $resourceName$). Log in.",
        SmsBody = "SMS: $fileTransferRecipient$ received $fileName$",
        ReminderEmailSubject = "Reminder for $fileTransferRecipient$",
        ReminderEmailBody = "Reminder: $fileTransferRecipient$ received $fileName$",
        ReminderSmsBody = "Reminder SMS: $fileTransferRecipient$ received $fileName$"
    };

    private static (
        Mock<IActorRepository> ActorRepository,
        Mock<IFileTransferNotificationRepository> NotificationRepository,
        Mock<INotificationTemplateRepository> TemplateRepository,
        Mock<IIdempotencyEventRepository> IdempotencyRepository,
        Mock<IAltinnRegisterService> RegisterService,
        Mock<IAltinnResourceRepository> ResourceRepository,
        CreateNotificationOrderHandler Handler) CreateHandler()
    {
        var actorRepository = new Mock<IActorRepository>();
        var notificationRepository = new Mock<IFileTransferNotificationRepository>();
        var templateRepository = new Mock<INotificationTemplateRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var registerService = new Mock<IAltinnRegisterService>();
        var resourceRepository = new Mock<IAltinnResourceRepository>();
        var logger = new Mock<ILogger<CreateNotificationOrderHandler>>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        templateRepository
            .Setup(r => r.GetNotificationTemplate(NotificationTemplate.GenericAltinnMessage, "nb", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultTemplate());
        registerService
            .Setup(s => s.LookupOrganizationName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test Organization AS");
        registerService
            .Setup(s => s.LookupOrganizationName("0192:991825827", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Sender AS");
        resourceRepository
            .Setup(r => r.GetResourceMetadata(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AltinnResourceMetadata { Title = "Test Resource" });
        actorRepository
            .Setup(r => r.GetActorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActorEntity?)null);
        actorRepository
            .Setup(r => r.AddActorAsync(It.IsAny<ActorEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreateNotificationOrderHandler(
            actorRepository.Object,
            notificationRepository.Object,
            templateRepository.Object,
            idempotencyRepository.Object,
            registerService.Object,
            resourceRepository.Object,
            logger.Object);

        return (actorRepository, notificationRepository, templateRepository, idempotencyRepository, registerService, resourceRepository, handler);
    }

    private static List<BrokerNotificationEntity> CapturePersisted(Mock<IFileTransferNotificationRepository> notificationRepository)
    {
        var persisted = new List<BrokerNotificationEntity>();
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted.Add(n))
            .Returns(Task.CompletedTask);
        return persisted;
    }

    private static NotificationOrderRequestV2 CapturedOrderRequest(BrokerNotificationEntity entity) =>
        JsonSerializer.Deserialize<NotificationOrderRequestV2>(entity.OrderRequest!)!;

    private static (EmailSettings? Email, SmsSettings? Sms) Settings(RecipientV2 recipient) =>
        (recipient.RecipientOrganization?.EmailSettings ?? recipient.RecipientPerson?.EmailSettings ?? recipient.RecipientEmail?.EmailSettings,
         recipient.RecipientOrganization?.SmsSettings ?? recipient.RecipientPerson?.SmsSettings ?? recipient.RecipientSms?.SmsSettings);

    [Fact]
    public async Task Process_WithNoRecipients_LogsWarningAndDoesNotPersistAnything()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var request = CreateRequest();

        await handler.Process(request, CancellationToken.None);

        notificationRepository.Verify(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithFileTransferRecipient_CreatesNewActorAndPersistsOneNotification()
    {
        var (actorRepository, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(recipientExternalIds: ["0192:123456789"]);

        await handler.Process(request, CancellationToken.None);

        actorRepository.Verify(r => r.AddActorAsync(It.Is<ActorEntity>(a => a.ActorExternalId == "0192:123456789"), It.IsAny<CancellationToken>()), Times.Once);
        var notification = Assert.Single(persisted);
        Assert.Equal(1, notification.ActorId);
        Assert.Null(notification.CustomRecipientType);
        Assert.Null(notification.CustomRecipientIdentifier);
        Assert.Null(notification.CustomRecipientRelatedOrganization);
        Assert.False(notification.IsReminder);
    }

    [Fact]
    public async Task Process_WithExistingActor_ReusesActorId_DoesNotCreateNewActor()
    {
        var (actorRepository, notificationRepository, _, _, _, _, handler) = CreateHandler();
        actorRepository
            .Setup(r => r.GetActorAsync("0192:123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorEntity { ActorId = 55, ActorExternalId = "0192:123456789" });
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(recipientExternalIds: ["0192:123456789"]);

        await handler.Process(request, CancellationToken.None);

        actorRepository.Verify(r => r.AddActorAsync(It.IsAny<ActorEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(55, Assert.Single(persisted).ActorId);
    }

    public static TheoryData<Recipient, CustomRecipientType, string> CustomRecipients => new()
    {
        { new Recipient { OrganizationNumber = "123456789" }, CustomRecipientType.Organization, "0192:123456789" },
        { new Recipient { OrganizationNumber = "0192:123456789" }, CustomRecipientType.Organization, "0192:123456789" },
        { new Recipient { OrganizationNumber = "urn:altinn:organization:identifier-no:123456789" }, CustomRecipientType.Organization, "0192:123456789" },
        { new Recipient { NationalIdentityNumber = ValidNationalIdentityNumber }, CustomRecipientType.Person, $"urn:altinn:person:identifier-no:{ValidNationalIdentityNumber}" },
        { new Recipient { NationalIdentityNumber = $"urn:altinn:person:identifier-no:{ValidNationalIdentityNumber}" }, CustomRecipientType.Person, $"urn:altinn:person:identifier-no:{ValidNationalIdentityNumber}" },
        { new Recipient { EmailAddress = "test@example.com" }, CustomRecipientType.Email, "test@example.com" },
        { new Recipient { MobileNumber = "+4799999999" }, CustomRecipientType.MobileNumber, "+4799999999" },
    };

    [Theory]
    [MemberData(nameof(CustomRecipients))]
    public async Task Process_WithCustomRecipient_PersistsTypeAndIdentifierWithoutTouchingActorTable(Recipient customRecipient, CustomRecipientType expectedType, string expectedIdentifier)
    {
        var (actorRepository, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(customRecipients: [customRecipient]);

        await handler.Process(request, CancellationToken.None);

        var notification = Assert.Single(persisted);
        Assert.Null(notification.ActorId);
        Assert.Equal(expectedType, notification.CustomRecipientType);
        Assert.Equal(expectedIdentifier, notification.CustomRecipientIdentifier);
        Assert.Equal($"0192:{DefaultRelatedOrganization}", notification.CustomRecipientRelatedOrganization);
        actorRepository.Verify(r => r.GetActorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        actorRepository.Verify(r => r.AddActorAsync(It.IsAny<ActorEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithDuplicateRecipientIdentifier_OnlyPersistsOnce()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var request = CreateRequest(
            recipientExternalIds: ["0192:123456789"],
            customRecipients: [new Recipient { OrganizationNumber = "123456789" }]);

        await handler.Process(request, CancellationToken.None);

        notificationRepository.Verify(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenIdempotencyClaimAlreadyTaken_SkipsRecipient()
    {
        var (_, notificationRepository, _, idempotencyRepository, _, _, handler) = CreateHandler();
        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);

        await handler.Process(request, CancellationToken.None);

        notificationRepository.Verify(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_ForEveryRecipientType_SendsIdenticalPlainTextOnRequestedChannel()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(
            recipientExternalIds: [$"0192:{DefaultRelatedOrganization}"],
            customRecipients:
            [
                new Recipient { OrganizationNumber = "222222222" },
                new Recipient { NationalIdentityNumber = ValidNationalIdentityNumber },
                new Recipient { EmailAddress = "test@example.com" },
                new Recipient { MobileNumber = "+4799999999" },
            ],
            sendReminder: true,
            channel: NotificationChannel.EmailAndSms);

        await handler.Process(request, CancellationToken.None);

        Assert.Equal(5, persisted.Count);
        Assert.All(persisted, n => Assert.Equal(NotificationTemplate.GenericAltinnMessage, n.NotificationTemplate));
        Assert.All(persisted, n => Assert.Equal(NotificationChannel.EmailAndSms, n.NotificationChannel));
        var orders = persisted.Select(CapturedOrderRequest).ToList();
        var main = orders.Select(o => Settings(o.Recipient)).ToList();
        var reminders = orders.Select(o => Settings(Assert.Single(o.Reminders!).Recipient)).ToList();
        foreach (var settings in new[] { main, reminders })
        {
            Assert.Single(settings.Where(s => s.Email is not null).Select(s => (s.Email!.Subject, s.Email.Body)).Distinct());
            Assert.Single(settings.Where(s => s.Sms is not null).Select(s => s.Sms!.Body).Distinct());
            Assert.All(settings.Where(s => s.Email is not null), s => Assert.Equal(EmailContentType.Plain, s.Email!.ContentType));
        }
        Assert.Equal("Message for Test Organization AS (111111111) from Sender AS", main.First(s => s.Email is not null).Email!.Subject);
    }

    [Fact]
    public async Task Process_ForCustomRecipient_SubstitutesItsOwnRelatedOrganizationAsFileTransferRecipient()
    {
        var (_, notificationRepository, _, _, registerService, _, handler) = CreateHandler();
        registerService
            .Setup(s => s.LookupOrganizationName("0192:222222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Org Two AS");
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(
            recipientExternalIds: ["0192:111111111", "0192:222222222"],
            customRecipients: [new Recipient { NationalIdentityNumber = ValidNationalIdentityNumber, RelatedOrganizationNumber = "0192:222222222" }]);

        await handler.Process(request, CancellationToken.None);

        var personNotification = persisted.Single(n => n.CustomRecipientType == CustomRecipientType.Person);
        Assert.Equal("0192:222222222", personNotification.CustomRecipientRelatedOrganization);
        Assert.Equal("Message for Org Two AS (222222222) from Sender AS", CapturedOrderRequest(personNotification).Recipient.RecipientPerson!.EmailSettings!.Subject);
    }

    [Fact]
    public async Task Process_WhenFileTransferRecipientNameIsUnknown_FallsBackToOrganizationNumber()
    {
        var (_, notificationRepository, _, _, registerService, _, handler) = CreateHandler();
        registerService
            .Setup(s => s.LookupOrganizationName("0192:222222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(
            recipientExternalIds: ["0192:222222222"],
            customRecipients: [new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = "222222222" }]);

        await handler.Process(request, CancellationToken.None);

        Assert.Equal(2, persisted.Count);
        Assert.All(persisted, n => Assert.Equal("Message for 222222222 from Sender AS", Settings(CapturedOrderRequest(n).Recipient).Email!.Subject));
    }

    [Fact]
    public async Task Process_WithSameCustomRecipientForTwoRelatedOrganizations_NotifiesOncePerRelatedOrganization()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(
            recipientExternalIds: ["0192:111111111", "0192:222222222"],
            customRecipients:
            [
                new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = "111111111" },
                new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = "222222222" },
                new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = "0192:222222222" },
            ]);

        await handler.Process(request, CancellationToken.None);

        var emailNotifications = persisted.Where(n => n.CustomRecipientType == CustomRecipientType.Email).ToList();
        Assert.Equal(["0192:111111111", "0192:222222222"], emailNotifications.Select(n => n.CustomRecipientRelatedOrganization).Order());
        Assert.Equal(2, emailNotifications.Select(n => CapturedOrderRequest(n).IdempotencyId).Distinct().Count());
    }

    [Fact]
    public async Task Process_WithFileTransferRecipientAndCustomRecipientsSharingIt_LooksUpItsNameOnlyOnce()
    {
        var (_, _, _, _, registerService, _, handler) = CreateHandler();
        var request = CreateRequest(
            recipientExternalIds: ["0192:222222222"],
            customRecipients:
            [
                new Recipient { EmailAddress = "first@example.com", RelatedOrganizationNumber = "222222222" },
                new Recipient { MobileNumber = "+4799999999", RelatedOrganizationNumber = "0192:222222222" },
                new Recipient { NationalIdentityNumber = ValidNationalIdentityNumber, RelatedOrganizationNumber = "urn:altinn:organization:identifier-no:222222222" },
            ]);

        await handler.Process(request, CancellationToken.None);

        registerService.Verify(s => s.LookupOrganizationName("0192:222222222", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WithCustomOrganizationRecipientThatIsAlsoAFileTransferRecipient_OnlyNotifiesItAsFileTransferRecipient()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(
            recipientExternalIds: ["0192:111111111"],
            customRecipients: [new Recipient { OrganizationNumber = "111111111", RelatedOrganizationNumber = "111111111" }]);

        await handler.Process(request, CancellationToken.None);

        var notification = Assert.Single(persisted);
        Assert.NotNull(notification.ActorId);
        Assert.Null(notification.CustomRecipientType);
    }

    [Theory]
    [InlineData(ValidNationalIdentityNumber)]
    [InlineData($"urn:altinn:person:identifier-no:{ValidNationalIdentityNumber}")]
    public async Task Process_ForCustomRecipientWithNationalIdentityNumber_SendsPersonNumberWithoutPrefix(string nationalIdentityNumber)
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(customRecipients: [new Recipient { NationalIdentityNumber = nationalIdentityNumber }], channel: NotificationChannel.EmailPreferred);

        await handler.Process(request, CancellationToken.None);

        var person = CapturedOrderRequest(Assert.Single(persisted)).Recipient.RecipientPerson!;
        Assert.Equal(ValidNationalIdentityNumber, person.NationalIdentityNumber);
        Assert.Equal("urn:altinn:resource:resource123", person.ResourceId);
        Assert.Equal(NotificationChannel.EmailPreferred, person.ChannelSchema);
        Assert.Equal("Test Organization AS (111111111) received document.pdf (resource: Test Resource). Log in.", person.EmailSettings!.Body);
    }

    [Fact]
    public async Task Process_WhenTemplateMissing_Throws()
    {
        var (_, notificationRepository, templateRepository, _, _, _, handler) = CreateHandler();
        templateRepository
            .Setup(r => r.GetNotificationTemplate(It.IsAny<NotificationTemplate>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationTemplateEntity?)null);
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Process(request, CancellationToken.None));

        notificationRepository.Verify(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithSendReminderTrue_AddsOneReminderWithSevenDayDelayAndSameChannel()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(
            recipientExternalIds: ["0192:123456789"],
            sendReminder: true,
            channel: NotificationChannel.SmsPreferred);

        await handler.Process(request, CancellationToken.None);

        var reminder = Assert.Single(CapturedOrderRequest(Assert.Single(persisted)).Reminders!);
        Assert.Equal(7, reminder.DelayDays);
        var reminderRecipient = reminder.Recipient.RecipientOrganization!;
        Assert.Equal(NotificationChannel.SmsPreferred, reminderRecipient.ChannelSchema);
        Assert.Equal("Reminder for Test Organization AS (123456789)", reminderRecipient.EmailSettings!.Subject);
        Assert.Equal("Reminder SMS: Test Organization AS (123456789) received document.pdf", reminderRecipient.SmsSettings!.Body);
    }

    [Fact]
    public async Task Process_WithoutSendReminder_OrderHasNoReminders()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var persisted = CapturePersisted(notificationRepository);
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }], sendReminder: false);

        await handler.Process(request, CancellationToken.None);

        Assert.Null(CapturedOrderRequest(Assert.Single(persisted)).Reminders);
    }
}
