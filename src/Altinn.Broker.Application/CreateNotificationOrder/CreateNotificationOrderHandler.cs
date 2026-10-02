using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Transactions;

using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
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

        var resolvedText = await ResolveNotificationText(notificationRequest, cancellationToken);
        var orderTokens = await ResolveOrderTokenValues(request, cancellationToken);
        var requestedSendTime = DateTimeOffset.UtcNow;
        var createdCount = 0;
        foreach (var (actorId, recipient) in recipients)
        {
            var recipientKey = BuildRecipientKey(recipient);
            using var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);

            var claimed = await idempotencyEventRepository.TryAddIdempotencyEventAsync(
                BuildNotificationClaimKey(request.FileTransferId, recipientKey),
                cancellationToken);
            if (!claimed)
            {
                logger.LogInformation(
                    "Notification order already created for file transfer {FileTransferId} and recipient {RecipientKey}. Skipping.",
                    request.FileTransferId,
                    recipientKey);
                transaction.Complete();
                continue;
            }

            var recipientName = await ResolveRecipientName(recipient, cancellationToken);
            var recipientNumber = ResolveRecipientNumber(recipient);
            var tokens = orderTokens with { RecipientName = recipientName, RecipientNumber = recipientNumber };
            var orderRequest = CreateNotificationOrderRequestV2(request.FileTransferId, request.ResourceId, recipient, recipientKey, notificationRequest, resolvedText, requestedSendTime, tokens);
            var notification = new BrokerNotificationEntity
            {
                Id = Guid.NewGuid(),
                FileTransferId = request.FileTransferId,
                ActorId = actorId,
                CustomRecipient = actorId is null ? JsonSerializer.Serialize(recipient) : null,
                NotificationTemplate = notificationRequest.NotificationTemplate,
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
            resolved.AddRange(notificationRequest.CustomRecipients.Select(recipient => ((long?)null, recipient)));
        }

        return resolved
            .GroupBy(entry => BuildRecipientKey(entry.Recipient))
            .Select(group => group.First())
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


    private async Task<ResolvedNotificationText> ResolveNotificationText(NotificationRequest notificationRequest, CancellationToken cancellationToken)
    {
        var callerText = new ResolvedNotificationText(
            notificationRequest.EmailSubject,
            notificationRequest.EmailBody,
            notificationRequest.SmsBody,
            notificationRequest.ReminderEmailSubject,
            notificationRequest.ReminderEmailBody,
            notificationRequest.ReminderSmsBody);

        if (notificationRequest.NotificationTemplate != NotificationTemplate.GenericAltinnMessage)
        {
            return callerText;
        }

        var language = notificationRequest.Language ?? DefaultLanguage;
        var template = await notificationTemplateRepository.GetNotificationTemplate(NotificationTemplate.GenericAltinnMessage, language, cancellationToken);
        if (template is null)
        {
            logger.LogWarning("No generic Altinn message template found for language {Language}. Falling back to the caller's own text only.", language.SanitizeForLogs());
            return callerText;
        }

        return new ResolvedNotificationText(
            MergeTemplateWithCustomText(template.EmailSubject, notificationRequest.EmailSubject),
            MergeTemplateWithCustomText(template.EmailBody, notificationRequest.EmailBody),
            MergeTemplateWithCustomText(template.SmsBody, notificationRequest.SmsBody),
            MergeTemplateWithCustomText(template.ReminderEmailSubject, notificationRequest.ReminderEmailSubject),
            MergeTemplateWithCustomText(template.ReminderEmailBody, notificationRequest.ReminderEmailBody),
            MergeTemplateWithCustomText(template.ReminderSmsBody, notificationRequest.ReminderSmsBody));
    }

    private static string? MergeTemplateWithCustomText(string? templateText, string? customText)
    {
        if (string.IsNullOrEmpty(templateText))
        {
            return customText;
        }
        return templateText.Replace("{textToken}", string.IsNullOrEmpty(customText) ? string.Empty : customText + " ").Trim();
    }

    /// <summary>
    /// Resolves the token values shared by every recipient's order for this file transfer - the sender's and
    /// resource's display names, and the file's own name/expiration (fetched once per <see cref="Process"/> call,
    /// not once per recipient, since none of them vary by recipient). Falls back to the raw identifier when a
    /// lookup fails, so a lookup outage degrades the notification text rather than blocking it from being created.
    /// </summary>
    private async Task<OrderTokenValues> ResolveOrderTokenValues(CreateNotificationOrderRequest request, CancellationToken cancellationToken)
    {
        var sendersName = await altinnRegisterService.LookupOrganizationName(request.SenderExternalId, cancellationToken)
            ?? request.SenderExternalId;
        var resourceMetadata = await altinnResourceRepository.GetResourceMetadata(request.ResourceId, cancellationToken);
        var resourceName = resourceMetadata?.Title ?? request.ResourceId;
        return new OrderTokenValues(sendersName, resourceName, request.FileName, request.FileTransferExpirationTime, RecipientName: string.Empty, RecipientNumber: string.Empty);
    }

    private async Task<string> ResolveRecipientName(Recipient recipient, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber))
        {
            var organizationExternalId = recipient.OrganizationNumber.WithPrefix();
            return await altinnRegisterService.LookupOrganizationName(organizationExternalId, cancellationToken) ?? recipient.OrganizationNumber;
        }
        return BuildActorExternalId(recipient);
    }

    private static string ResolveRecipientNumber(Recipient recipient) =>
        !string.IsNullOrEmpty(recipient.OrganizationNumber) ? recipient.OrganizationNumber : BuildActorExternalId(recipient);

    private static string BuildActorExternalId(Recipient recipient)
    {
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber)) return recipient.OrganizationNumber.WithPrefix();
        if (!string.IsNullOrEmpty(recipient.EmailAddress)) return recipient.EmailAddress;
        if (!string.IsNullOrEmpty(recipient.MobileNumber)) return recipient.MobileNumber;
        throw new InvalidOperationException("Recipient must have exactly one identifier");
    }

    private static string BuildRecipientKey(Recipient recipient) => BuildActorExternalId(recipient);

    private static string BuildNotificationClaimKey(Guid fileTransferId, string recipientKey) => $"{fileTransferId}_notification_{recipientKey}";

    private static Guid CreateStableIdempotencyId(Guid fileTransferId, string recipientKey)
    {
        var name = $"notification:{fileTransferId}:{recipientKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static NotificationOrderRequestV2 CreateNotificationOrderRequestV2(
        Guid fileTransferId,
        string resourceId,
        Recipient recipient,
        string recipientKey,
        NotificationRequest notificationRequest,
        ResolvedNotificationText resolvedText,
        DateTimeOffset requestedSendTime,
        OrderTokenValues tokens)
    {
        var order = new NotificationOrderRequestV2
        {
            SendersReference = $"bro-{fileTransferId}",
            RequestedSendTime = requestedSendTime.UtcDateTime,
            IdempotencyId = CreateStableIdempotencyId(fileTransferId, recipientKey),
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
            .Replace("$recipientName$", tokens.RecipientName)
            .Replace("$recipientNumber$", tokens.RecipientNumber)
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
        var channel = isReminder
            ? notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel
            : notificationRequest.NotificationChannel;
        var emailSubject = ApplyTokens(isReminder ? resolvedText.ReminderEmailSubject : resolvedText.EmailSubject, tokens);
        var emailBody = ApplyTokens(isReminder ? resolvedText.ReminderEmailBody : resolvedText.EmailBody, tokens);
        var smsBody = ApplyTokens(isReminder ? resolvedText.ReminderSmsBody : resolvedText.SmsBody, tokens);
        var emailContentType = isReminder
            ? notificationRequest.ReminderEmailContentType ?? notificationRequest.EmailContentType
            : notificationRequest.EmailContentType;

        var emailSettings = !string.IsNullOrWhiteSpace(emailSubject) && !string.IsNullOrWhiteSpace(emailBody)
            ? new EmailSettings { Subject = emailSubject, Body = emailBody, ContentType = emailContentType }
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
    /// The token values substitutable into the final notification text via $sendersName$/$recipientName$/
    /// $recipientNumber$/$resourceName$/$fileName$/$expirationTime$. <see cref="RecipientName"/> and
    /// <see cref="RecipientNumber"/> are the only ones that vary per recipient.
    /// </summary>
    private sealed record OrderTokenValues(string SendersName, string ResourceName, string FileName, DateTime ExpirationTime, string RecipientName, string RecipientNumber);

    /// <summary>
    /// The final text to send, after resolving <see cref="NotificationTemplate.CustomMessage"/> vs.
    /// <see cref="NotificationTemplate.GenericAltinnMessage"/> - but before $token$ substitution, which is applied
    /// per recipient in <see cref="CreateRecipientV2"/>.
    /// </summary>
    private sealed record ResolvedNotificationText(
        string? EmailSubject,
        string? EmailBody,
        string? SmsBody,
        string? ReminderEmailSubject,
        string? ReminderEmailBody,
        string? ReminderSmsBody);
}
