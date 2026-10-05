using System.Text.Json;
using System.Transactions;

using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Common;
using Altinn.Broker.Common.Constants;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.CreateNotificationOrder;

public class CreateNotificationOrderHandler(
    IActorRepository actorRepository,
    IFileTransferNotificationRepository fileTransferNotificationRepository,
    INotificationTemplateRepository notificationTemplateRepository,
    IIdempotencyEventRepository idempotencyEventRepository,
    IAltinnRegisterService altinnRegisterService,
    IAltinnResourceRepository altinnResourceRepository,
    ILogger<CreateNotificationOrderHandler> logger) : ICreateNotificationOrderHandler
{
    private const int ReminderDelayDays = 7;
    private const string DefaultLanguage = "nb";
    private const EmailContentType DefaultEmailContentType = EmailContentType.Plain;
    private const string FileTransferRecipientToken = "$fileTransferRecipient$";
    private static readonly string[] NotificationsResolvedTokens = ["$recipientName$", "$recipientNumber$"];

    public async Task Process(CreateNotificationOrderRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification order creation for file transfer {FileTransferId}", request.FileTransferId);

        var notificationRequest = request.NotificationRequest;
        var recipients = await ResolveRecipients(request, notificationRequest, cancellationToken);
        if (recipients.Count == 0)
        {
            logger.LogWarning("No recipients resolved for notification order on file transfer {FileTransferId}", request.FileTransferId);
            return;
        }

        var resolvedText = await ResolveNotificationText(cancellationToken);
        var orderTokens = await ResolveOrderTokenValues(request, cancellationToken);
        var fileTransferRecipients = await ResolveFileTransferRecipients(recipients, cancellationToken);
        var requestedSendTime = DateTimeOffset.UtcNow;
        var createdCount = 0;
        for (var recipientIndex = 0; recipientIndex < recipients.Count; recipientIndex++)
        {
            var (actorId, recipient) = recipients[recipientIndex];
            var notificationId = Guid.NewGuid();
            using var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);

            var claimed = await idempotencyEventRepository.TryAddIdempotencyEventAsync(
                BuildNotificationClaimKey(request.FileTransferId, recipientIndex),
                cancellationToken);
            if (!claimed)
            {
                logger.LogInformation(
                    "Notification order for recipient {RecipientIndex} already created for file transfer {FileTransferId}. Skipping.",
                    recipientIndex,
                    request.FileTransferId);
                transaction.Complete();
                continue;
            }

            var tokens = orderTokens with
            {
                FileTransferRecipient = fileTransferRecipients[GetFileTransferRecipientNumber(actorId, recipient)]
            };
            var relatedOrganizationNumber = recipient.RelatedOrganizationNumber?.WithoutPrefix();
            var orderRequest = CreateNotificationOrderRequestV2(request.FileTransferId, request.ResourceId, recipient, notificationId, notificationRequest, resolvedText, requestedSendTime, tokens);
            var (customRecipientType, customRecipientIdentifier) = actorId is null ? DescribeCustomRecipient(recipient) : (null, null);
            var notification = new BrokerNotificationEntity
            {
                Id = notificationId,
                FileTransferId = request.FileTransferId,
                ActorId = actorId,
                CustomRecipientType = customRecipientType,
                CustomRecipientIdentifier = customRecipientIdentifier,
                CustomRecipientRelatedOrganization = actorId is null ? relatedOrganizationNumber?.WithPrefix() : null,
                NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                NotificationChannel = notificationRequest.NotificationChannel,
                RequestedSendTime = requestedSendTime,
                Created = DateTimeOffset.UtcNow,
                IsReminder = false,
                OrderRequest = JsonSerializer.Serialize(orderRequest)
            };
            await fileTransferNotificationRepository.AddNotification(notification, cancellationToken);
            transaction.Complete();
            createdCount++;
        }

        logger.LogInformation("Created {Count} notification order(s) for file transfer {FileTransferId}", createdCount, request.FileTransferId);
    }

    private async Task<List<(long? ActorId, Recipient Recipient)>> ResolveRecipients(
        CreateNotificationOrderRequest request,
        NotificationRequest notificationRequest,
        CancellationToken cancellationToken)
    {
        var resolved = new List<(long? ActorId, Recipient Recipient)>();

        foreach (var externalId in request.RecipientExternalIds)
        {
            var recipient = new Recipient { OrganizationNumber = externalId.WithoutPrefix() };
            var actorId = await ResolveActorId(recipient, cancellationToken);
            resolved.Add((actorId, recipient));
        }

        if (notificationRequest.CustomRecipients is { Count: > 0 })
        {
            var fileTransferRecipients = request.RecipientExternalIds.Select(id => id.WithoutPrefix()).ToHashSet();
            resolved.AddRange(notificationRequest.CustomRecipients
                .Where(recipient => string.IsNullOrEmpty(recipient.OrganizationNumber) || !fileTransferRecipients.Contains(recipient.OrganizationNumber.WithoutPrefix()))
                .Select(recipient => ((long?)null, recipient)));
        }

        return resolved
            .GroupBy(entry => BuildRecipientKey(entry.Recipient))
            .Select(group => group.First())
            .OrderBy(entry => BuildRecipientKey(entry.Recipient), StringComparer.Ordinal)
            .ToList();
    }

    private async Task<long> ResolveActorId(Recipient recipient, CancellationToken cancellationToken)
    {
        var externalId = BuildActorExternalId(recipient);
        var actor = await actorRepository.GetActorAsync(externalId, cancellationToken);
        if (actor is not null)
        {
            return actor.ActorId;
        }
        return await actorRepository.AddActorAsync(new ActorEntity { ActorExternalId = externalId }, cancellationToken);
    }

    private async Task<ResolvedNotificationText> ResolveNotificationText(CancellationToken cancellationToken)
    {
        var template = await notificationTemplateRepository.GetNotificationTemplate(NotificationTemplate.GenericAltinnMessage, DefaultLanguage, cancellationToken)
            ?? throw new InvalidOperationException($"No {NotificationTemplate.GenericAltinnMessage} notification template found for language '{DefaultLanguage}'");

        var resolvedText = new ResolvedNotificationText(
            template.EmailSubject,
            template.EmailBody,
            template.SmsBody,
            template.ReminderEmailSubject,
            template.ReminderEmailBody,
            template.ReminderSmsBody);

        if (resolvedText.ContainsAny(NotificationsResolvedTokens))
        {
            throw new InvalidOperationException($"The {NotificationTemplate.GenericAltinnMessage} notification template must not contain {string.Join("/", NotificationsResolvedTokens)}");
        }

        return resolvedText;
    }

    private async Task<OrderTokenValues> ResolveOrderTokenValues(CreateNotificationOrderRequest request, CancellationToken cancellationToken)
    {
        var sendersName = await altinnRegisterService.LookupOrganizationName(request.SenderExternalId, cancellationToken)
            ?? request.SenderExternalId;
        var resourceMetadata = await altinnResourceRepository.GetResourceMetadata(request.ResourceId, cancellationToken);
        var resourceName = resourceMetadata?.Title ?? request.ResourceId;
        return new OrderTokenValues(sendersName, resourceName, request.FileName, request.FileTransferExpirationTime, FileTransferRecipient: string.Empty);
    }

    private static string GetFileTransferRecipientNumber(long? actorId, Recipient recipient) =>
        actorId is null ? recipient.RelatedOrganizationNumber!.WithoutPrefix() : recipient.OrganizationNumber!.WithoutPrefix();

    private async Task<Dictionary<string, string>> ResolveFileTransferRecipients(List<(long? ActorId, Recipient Recipient)> recipients, CancellationToken cancellationToken)
    {
        var descriptions = new Dictionary<string, string>();
        var organizationNumbers = recipients
            .Select(entry => GetFileTransferRecipientNumber(entry.ActorId, entry.Recipient))
            .Distinct();
        foreach (var organizationNumber in organizationNumbers)
        {
            var name = await altinnRegisterService.LookupOrganizationName(organizationNumber.WithPrefix(), cancellationToken);
            descriptions[organizationNumber] = string.IsNullOrWhiteSpace(name) ? organizationNumber : $"{name} ({organizationNumber})";
        }
        return descriptions;
    }

    private static (CustomRecipientType? Type, string? Identifier) DescribeCustomRecipient(Recipient recipient)
    {
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber)) return (CustomRecipientType.Organization, BuildActorExternalId(recipient));
        if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber)) return (CustomRecipientType.Person, BuildActorExternalId(recipient));
        if (!string.IsNullOrEmpty(recipient.EmailAddress)) return (CustomRecipientType.Email, recipient.EmailAddress);
        if (!string.IsNullOrEmpty(recipient.MobileNumber)) return (CustomRecipientType.MobileNumber, recipient.MobileNumber);
        throw new InvalidOperationException("Recipient must have exactly one identifier");
    }

    private static string BuildActorExternalId(Recipient recipient)
    {
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber)) return recipient.OrganizationNumber.WithoutPrefix().WithPrefix();
        if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber)) return $"{UrnConstants.PersonIdAttribute}:{recipient.NationalIdentityNumber.WithoutPrefix()}";
        if (!string.IsNullOrEmpty(recipient.EmailAddress)) return recipient.EmailAddress;
        if (!string.IsNullOrEmpty(recipient.MobileNumber)) return recipient.MobileNumber;
        throw new InvalidOperationException("Recipient must have exactly one identifier");
    }

    private static string BuildRecipientKey(Recipient recipient) =>
        string.IsNullOrEmpty(recipient.RelatedOrganizationNumber)
            ? BuildActorExternalId(recipient)
            : $"{BuildActorExternalId(recipient)}_{recipient.RelatedOrganizationNumber.WithoutPrefix().WithPrefix()}";

    private static string BuildNotificationClaimKey(Guid fileTransferId, int recipientIndex) =>
        $"{fileTransferId}_notification_{recipientIndex}";

    private static NotificationOrderRequestV2 CreateNotificationOrderRequestV2(
        Guid fileTransferId,
        string resourceId,
        Recipient recipient,
        Guid notificationId,
        NotificationRequest notificationRequest,
        ResolvedNotificationText resolvedText,
        DateTimeOffset requestedSendTime,
        OrderTokenValues tokens)
    {
        var order = new NotificationOrderRequestV2
        {
            SendersReference = $"bro-{fileTransferId}",
            RequestedSendTime = requestedSendTime.UtcDateTime,
            IdempotencyId = notificationId,
            Recipient = CreateRecipientV2(resourceId, recipient, notificationRequest, resolvedText, tokens, isReminder: false)
        };

        if (notificationRequest.SendReminder)
        {
            order.Reminders =
            [
                new ReminderV2
                {
                    SendersReference = order.SendersReference,
                    DelayDays = ReminderDelayDays,
                    Recipient = CreateRecipientV2(resourceId, recipient, notificationRequest, resolvedText, tokens, isReminder: true)
                }
            ];
        }

        return order;
    }

    private static string? ApplyTokens(string? text, OrderTokenValues tokens) =>
        text?
            .Replace("$sendersName$", tokens.SendersName)
            .Replace(FileTransferRecipientToken, tokens.FileTransferRecipient)
            .Replace("$resourceName$", tokens.ResourceName)
            .Replace("$fileName$", tokens.FileName)
            .Replace("$expirationTime$", tokens.ExpirationTime.ToString("dd.MM.yyyy HH:mm"));

    private static RecipientV2 CreateRecipientV2(
        string resourceId,
        Recipient recipient,
        NotificationRequest notificationRequest,
        ResolvedNotificationText resolvedText,
        OrderTokenValues tokens,
        bool isReminder)
    {
        var resourceIdWithPrefix = $"urn:altinn:resource:{resourceId}";
        var channel = notificationRequest.NotificationChannel;
        var emailSubject = ApplyTokens(isReminder ? resolvedText.ReminderEmailSubject : resolvedText.EmailSubject, tokens);
        var emailBody = ApplyTokens(isReminder ? resolvedText.ReminderEmailBody : resolvedText.EmailBody, tokens);
        var smsBody = ApplyTokens(isReminder ? resolvedText.ReminderSmsBody : resolvedText.SmsBody, tokens);

        var emailSettings = !string.IsNullOrWhiteSpace(emailSubject) && !string.IsNullOrWhiteSpace(emailBody)
            ? new EmailSettings { Subject = emailSubject, Body = emailBody, ContentType = DefaultEmailContentType }
            : null;
        var smsSettings = !string.IsNullOrWhiteSpace(smsBody)
            ? new SmsSettings { Body = smsBody }
            : null;

        if (!string.IsNullOrEmpty(recipient.OrganizationNumber))
        {
            return new RecipientV2
            {
                RecipientOrganization = new RecipientOrganization
                {
                    OrgNumber = recipient.OrganizationNumber,
                    ResourceId = resourceIdWithPrefix,
                    ChannelSchema = channel,
                    EmailSettings = emailSettings,
                    SmsSettings = smsSettings
                }
            };
        }
        else if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber))
        {
            return new RecipientV2
            {
                RecipientPerson = new RecipientPerson
                {
                    NationalIdentityNumber = recipient.NationalIdentityNumber.WithoutPrefix(),
                    ResourceId = resourceIdWithPrefix,
                    ChannelSchema = channel,
                    EmailSettings = emailSettings,
                    SmsSettings = smsSettings
                }
            };
        }
        else if (!string.IsNullOrEmpty(recipient.EmailAddress))
        {
            return new RecipientV2
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = recipient.EmailAddress,
                    EmailSettings = emailSettings
                }
            };
        }
        else if (!string.IsNullOrEmpty(recipient.MobileNumber))
        {
            return new RecipientV2
            {
                RecipientSms = new RecipientSms
                {
                    PhoneNumber = recipient.MobileNumber,
                    SmsSettings = smsSettings
                }
            };
        }

        throw new InvalidOperationException("Recipient must have exactly one identifier");
    }

    /// <summary>
    /// The token values substitutable into the final notification text via $sendersName$/$fileTransferRecipient$/
    /// $resourceName$/$fileName$/$expirationTime$. <see cref="FileTransferRecipient"/> is the only one that varies per
    /// recipient.
    /// </summary>
    private sealed record OrderTokenValues(string SendersName, string ResourceName, string FileName, DateTime ExpirationTime, string FileTransferRecipient);

    /// <summary>
    /// The template text to send, before $token$ substitution, which is applied per recipient in
    /// <see cref="CreateRecipientV2"/>.
    /// </summary>
    private sealed record ResolvedNotificationText(
        string? EmailSubject,
        string? EmailBody,
        string? SmsBody,
        string? ReminderEmailSubject,
        string? ReminderEmailBody,
        string? ReminderSmsBody)
    {
        public bool ContainsAny(IEnumerable<string> tokens) =>
            new[] { EmailSubject, EmailBody, SmsBody, ReminderEmailSubject, ReminderEmailBody, ReminderSmsBody }
                .Any(text => text is not null && tokens.Any(token => text.Contains(token, StringComparison.Ordinal)));
    }
}
