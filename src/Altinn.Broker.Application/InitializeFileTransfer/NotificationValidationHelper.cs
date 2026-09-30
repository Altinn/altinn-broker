using System.Text.RegularExpressions;

using Altinn.Broker.Application.CreateNotificationOrder;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;

namespace Altinn.Broker.Application.InitializeFileTransfer;

public static class NotificationValidationHelper
{
    private static readonly Regex EmailRegex = new Regex(@"^((""[^""]+"")|(([a-zA-Z0-9æøåÆØÅ!#$%&'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9æøåÆØÅ!#$%&'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9æøåÆØÅ]([a-zA-Z0-9\-æøåÆØÅ]{0,61})[a-zA-Z0-9æøåÆØÅ]\.)|[a-zA-Z0-9æøåÆØÅ]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))$");

    public static Error? Validate(NotificationRequest notification)
    {

        if (notification.NotificationTemplate != NotificationTemplate.GenericAltinnMessage)
        {
            var contentError = ValidateChannelContent(
                notification.NotificationChannel,
                notification.EmailSubject,
                notification.EmailBody,
                notification.SmsBody,
                isReminder: false);
            if (contentError is not null)
            {
                return contentError;
            }

            if (notification.SendReminder)
            {
                var reminderChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel;
                var reminderContentError = ValidateChannelContent(
                    reminderChannel,
                    notification.ReminderEmailSubject,
                    notification.ReminderEmailBody,
                    notification.ReminderSmsBody,
                    isReminder: true);
                if (reminderContentError is not null)
                {
                    return reminderContentError;
                }
            }
        }

        if (notification.CustomRecipients is { Count: > 0 })
        {
            var usesRecipientNameOrNumberKeyword = AnyTextContainsKeyword(notification, "$recipientName$")
                || AnyTextContainsKeyword(notification, "$recipientNumber$");

            foreach (var recipient in notification.CustomRecipients)
            {
                var recipientError = ValidateCustomRecipientIdentifier(recipient);
                if (recipientError is not null)
                {
                    return recipientError;
                }

                var identifiedByEmailOrMobile = !string.IsNullOrEmpty(recipient.EmailAddress) || !string.IsNullOrEmpty(recipient.MobileNumber);
                if (identifiedByEmailOrMobile && usesRecipientNameOrNumberKeyword)
                {
                    return NotificationErrors.CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName;
                }
            }
        }

        return null;
    }

    private static bool AnyTextContainsKeyword(NotificationRequest notification, string keyword) =>
        ContainsKeyword(notification.EmailSubject, keyword)
        || ContainsKeyword(notification.EmailBody, keyword)
        || ContainsKeyword(notification.SmsBody, keyword)
        || ContainsKeyword(notification.ReminderEmailSubject, keyword)
        || ContainsKeyword(notification.ReminderEmailBody, keyword)
        || ContainsKeyword(notification.ReminderSmsBody, keyword);

    private static bool ContainsKeyword(string? text, string keyword) =>
        !string.IsNullOrEmpty(text) && text.Contains(keyword, StringComparison.Ordinal);

    private static Error? ValidateChannelContent(NotificationChannel channel, string? emailSubject, string? emailBody, string? smsBody, bool isReminder)
    {
        var hasEmailContent = !string.IsNullOrWhiteSpace(emailSubject) && !string.IsNullOrWhiteSpace(emailBody);
        var hasSmsContent = !string.IsNullOrWhiteSpace(smsBody);

        return channel switch
        {
            NotificationChannel.Email when !hasEmailContent =>
                isReminder ? NotificationErrors.MissingEmailReminderContent : NotificationErrors.MissingEmailContent,
            NotificationChannel.Sms when !hasSmsContent =>
                isReminder ? NotificationErrors.MissingSmsReminderContent : NotificationErrors.MissingSmsContent,
            NotificationChannel.EmailAndSms when !hasEmailContent || !hasSmsContent =>
                isReminder ? NotificationErrors.MissingEmailAndSmsReminderContent : NotificationErrors.MissingEmailAndSmsContent,
            NotificationChannel.EmailPreferred or NotificationChannel.SmsPreferred when !hasEmailContent || !hasSmsContent =>
                isReminder ? NotificationErrors.MissingPreferredReminderChannel : NotificationErrors.MissingPreferredChannel,
            _ => null
        };
    }

    private static Error? ValidateCustomRecipientIdentifier(Recipient recipient)
    {
        var identifierCount = new[] { recipient.OrganizationNumber, recipient.EmailAddress, recipient.MobileNumber }
            .Count(identifier => !string.IsNullOrEmpty(identifier));

        if (identifierCount == 0)
        {
            return NotificationErrors.CustomRecipientWithoutIdentifierNotAllowed;
        }
        if (identifierCount > 1)
        {
            return NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed;
        }

        if (recipient.EmailAddress is not null && (!EmailRegex.IsMatch(recipient.EmailAddress) || recipient.EmailAddress.Contains(';')))
        {
            return NotificationErrors.InvalidEmailProvided;
        }
        if (recipient.MobileNumber is not null && !MobileNumberHelper.IsValidMobileNumber(recipient.MobileNumber))
        {
            return NotificationErrors.InvalidMobileNumberProvided;
        }

        return null;
    }
}
