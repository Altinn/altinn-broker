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
    private static CreateNotificationOrderRequest CreateRequest(
        List<string>? recipientExternalIds = null,
        List<Recipient>? customRecipients = null,
        NotificationTemplate template = NotificationTemplate.CustomMessage,
        string? emailSubject = "subject",
        string? emailBody = "body",
        string? smsBody = "sms",
        bool sendReminder = false,
        string? language = null,
        NotificationChannel channel = NotificationChannel.Email) => new()
    {
        FileTransferId = Guid.NewGuid(),
        ResourceId = "resource123",
        SenderExternalId = "0192:991825827",
        FileName = "document.pdf",
        RecipientExternalIds = recipientExternalIds ?? [],
        FileTransferExpirationTime = DateTime.UtcNow.AddDays(30),
        NotificationRequest = new NotificationRequest
        {
            NotificationTemplate = template,
            NotificationChannel = channel,
            EmailSubject = emailSubject,
            EmailBody = emailBody,
            SmsBody = smsBody,
            SendReminder = sendReminder,
            Language = language,
            CustomRecipients = customRecipients
        }
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
        registerService
            .Setup(s => s.LookupOrganizationName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test Organization AS");
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

    private static NotificationOrderRequestV2 CapturedOrderRequest(BrokerNotificationEntity entity) =>
        JsonSerializer.Deserialize<NotificationOrderRequestV2>(entity.OrderRequest!)!;

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
        var request = CreateRequest(recipientExternalIds: ["0192:123456789"]);
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        actorRepository.Verify(r => r.AddActorAsync(It.Is<ActorEntity>(a => a.ActorExternalId == "0192:123456789"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(persisted);
        Assert.Equal(1, persisted!.ActorId);
        Assert.Null(persisted.CustomRecipient);
        Assert.False(persisted.IsReminder);
    }

    [Fact]
    public async Task Process_WithExistingActor_ReusesActorId_DoesNotCreateNewActor()
    {
        var (actorRepository, notificationRepository, _, _, _, _, handler) = CreateHandler();
        actorRepository
            .Setup(r => r.GetActorAsync("0192:123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorEntity { ActorId = 55, ActorExternalId = "0192:123456789" });
        var request = CreateRequest(recipientExternalIds: ["0192:123456789"]);
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        actorRepository.Verify(r => r.AddActorAsync(It.IsAny<ActorEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(55, persisted!.ActorId);
    }

    [Fact]
    public async Task Process_WithCustomRecipient_PersistsWithNullActorIdAndSerializedRecipient()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Null(persisted!.ActorId);
        Assert.NotNull(persisted.CustomRecipient);
        var deserializedRecipient = JsonSerializer.Deserialize<Recipient>(persisted.CustomRecipient!);
        Assert.Equal("test@example.com", deserializedRecipient!.EmailAddress);
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
    public async Task Process_WithCustomMessageTemplate_NeverLooksUpTemplateAndUsesCallerTextDirectly()
    {
        var (_, notificationRepository, templateRepository, _, _, _, handler) = CreateHandler();
        var request = CreateRequest(
            customRecipients: [new Recipient { EmailAddress = "test@example.com" }],
            template: NotificationTemplate.CustomMessage,
            emailSubject: "My custom subject",
            emailBody: "My custom body");
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        templateRepository.Verify(r => r.GetNotificationTemplate(It.IsAny<NotificationTemplate>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var order = CapturedOrderRequest(persisted!);
        Assert.Equal("My custom subject", order.Recipient.RecipientEmail!.EmailSettings!.Subject);
        Assert.Equal("My custom body", order.Recipient.RecipientEmail.EmailSettings.Body);
    }

    [Fact]
    public async Task Process_WithGenericAltinnMessageTemplate_MergesCustomTextIntoTextToken()
    {
        var (_, notificationRepository, templateRepository, _, _, _, handler) = CreateHandler();
        templateRepository
            .Setup(r => r.GetNotificationTemplate(NotificationTemplate.GenericAltinnMessage, "nb", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationTemplateEntity
            {
                Id = 1,
                NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                Language = "nb",
                EmailSubject = "You have a message",
                EmailBody = "Hello. {textToken}Regards, $sendersName$",
                SmsBody = "SMS: {textToken}"
            });
        var request = CreateRequest(
            customRecipients: [new Recipient { EmailAddress = "test@example.com" }],
            template: NotificationTemplate.GenericAltinnMessage,
            emailSubject: null,
            emailBody: "Please review the attached file.",
            smsBody: null);
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        var order = CapturedOrderRequest(persisted!);
        Assert.Equal(
            "Hello. Please review the attached file. Regards, Test Organization AS",
            order.Recipient.RecipientEmail!.EmailSettings!.Body);
    }

    [Fact]
    public async Task Process_WithGenericAltinnMessageAndNoTemplateFound_FallsBackToCallerTextOnly()
    {
        var (_, notificationRepository, templateRepository, _, _, _, handler) = CreateHandler();
        templateRepository
            .Setup(r => r.GetNotificationTemplate(It.IsAny<NotificationTemplate>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationTemplateEntity?)null);
        var request = CreateRequest(
            customRecipients: [new Recipient { EmailAddress = "test@example.com" }],
            template: NotificationTemplate.GenericAltinnMessage,
            emailSubject: "Fallback subject",
            emailBody: "Fallback body");
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        var order = CapturedOrderRequest(persisted!);
        Assert.Equal("Fallback subject", order.Recipient.RecipientEmail!.EmailSettings!.Subject);
        Assert.Equal("Fallback body", order.Recipient.RecipientEmail.EmailSettings.Body);
    }

    [Fact]
    public async Task Process_ForOrganizationRecipient_SubstitutesSendersNameResourceNameFileNameAndRecipientNumberWithoutPrefix()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var request = CreateRequest(
            recipientExternalIds: ["0192:123456789"],
            emailSubject: "Message from $sendersName$",
            emailBody: "Resource: $resourceName$, File: $fileName$, Your number: $recipientNumber$");
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        var order = CapturedOrderRequest(persisted!);
        Assert.Equal("Message from Test Organization AS", order.Recipient.RecipientOrganization!.EmailSettings!.Subject);
        Assert.Equal("Resource: Test Resource, File: document.pdf, Your number: 123456789", order.Recipient.RecipientOrganization.EmailSettings.Body);
    }

    [Fact]
    public async Task Process_WithSendReminderTrue_AddsOneReminderWithSevenDayDelay()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var request = CreateRequest(
            customRecipients: [new Recipient { EmailAddress = "test@example.com" }],
            sendReminder: true);
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        var order = CapturedOrderRequest(persisted!);
        var reminder = Assert.Single(order.Reminders!);
        Assert.Equal(7, reminder.DelayDays);
    }

    [Fact]
    public async Task Process_WithoutSendReminder_OrderHasNoReminders()
    {
        var (_, notificationRepository, _, _, _, _, handler) = CreateHandler();
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }], sendReminder: false);
        BrokerNotificationEntity? persisted = null;
        notificationRepository
            .Setup(r => r.AddNotification(It.IsAny<BrokerNotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persisted = n)
            .Returns(Task.CompletedTask);

        await handler.Process(request, CancellationToken.None);

        var order = CapturedOrderRequest(persisted!);
        Assert.Null(order.Reminders);
    }
}
