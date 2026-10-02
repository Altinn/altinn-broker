using System.ComponentModel;

using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class InitializeFileTransferMapper
{
    internal static InitializeFileTransferRequest MapToRequest(FileTransferInitalizeExt fileTransferInitializeExt)
    {
        return new InitializeFileTransferRequest()
        {
            ResourceId = fileTransferInitializeExt.ResourceId.WithoutPrefix(),
            FileName = fileTransferInitializeExt.FileName,
            SenderExternalId = fileTransferInitializeExt.Sender,
            SendersFileTransferReference = fileTransferInitializeExt.SendersFileTransferReference,
            PropertyList = fileTransferInitializeExt.PropertyList,
            RecipientExternalIds = fileTransferInitializeExt.Recipients,
            Checksum = fileTransferInitializeExt.Checksum,
            DisableVirusScan = fileTransferInitializeExt.DisableVirusScan ?? false,
            Notification = MapNotification(fileTransferInitializeExt.Notification)
        };
    }

    private static NotificationRequest? MapNotification(NotificationRequestExt? notification)
    {
        if (notification is null)
        {
            return null;
        }

        return new NotificationRequest()
        {
            NotificationTemplate = MapNotificationTemplate(notification.NotificationTemplate),
            EmailSubject = notification.EmailSubject,
            EmailBody = notification.EmailBody,
            EmailContentType = MapEmailContentType(notification.EmailContentType),
            SmsBody = notification.SmsBody,
            SendReminder = notification.SendReminder,
            ReminderEmailSubject = notification.ReminderEmailSubject,
            ReminderEmailBody = notification.ReminderEmailBody,
            ReminderEmailContentType = notification.ReminderEmailContentType is null ? null : MapEmailContentType(notification.ReminderEmailContentType.Value),
            ReminderSmsBody = notification.ReminderSmsBody,
            NotificationChannel = MapNotificationChannel(notification.NotificationChannel),
            ReminderNotificationChannel = notification.ReminderNotificationChannel is null ? null : MapNotificationChannel(notification.ReminderNotificationChannel.Value),
            Language = notification.Language,
            CustomRecipients = notification.CustomRecipients?.Select(MapCustomRecipient).ToList(),
        };
    }

    private static Recipient MapCustomRecipient(NotificationRecipientExt recipient) => new()
    {
        EmailAddress = recipient.EmailAddress,
        MobileNumber = recipient.MobileNumber,
        OrganizationNumber = recipient.OrganizationNumber,
    };

    private static NotificationTemplate MapNotificationTemplate(NotificationTemplateExt template) => template switch
    {
        NotificationTemplateExt.CustomMessage => NotificationTemplate.CustomMessage,
        NotificationTemplateExt.GenericAltinnMessage => NotificationTemplate.GenericAltinnMessage,
        _ => throw new InvalidEnumArgumentException(nameof(template), (int)template, typeof(NotificationTemplateExt))
    };

    private static NotificationChannel MapNotificationChannel(NotificationChannelExt channel) => channel switch
    {
        NotificationChannelExt.Email => NotificationChannel.Email,
        NotificationChannelExt.Sms => NotificationChannel.Sms,
        NotificationChannelExt.EmailPreferred => NotificationChannel.EmailPreferred,
        NotificationChannelExt.SmsPreferred => NotificationChannel.SmsPreferred,
        NotificationChannelExt.EmailAndSms => NotificationChannel.EmailAndSms,
        _ => throw new InvalidEnumArgumentException(nameof(channel), (int)channel, typeof(NotificationChannelExt))
    };

    private static EmailContentType MapEmailContentType(EmailContentTypeExt contentType) => contentType switch
    {
        EmailContentTypeExt.Plain => EmailContentType.Plain,
        EmailContentTypeExt.Html => EmailContentType.Html,
        _ => throw new InvalidEnumArgumentException(nameof(contentType), (int)contentType, typeof(EmailContentTypeExt))
    };
}
