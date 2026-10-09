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
            NotificationChannel = MapNotificationChannel(notification.NotificationChannel),
            SendReminder = notification.SendReminder,
            CustomRecipients = notification.CustomRecipients?.Select(MapCustomRecipient).ToList(),
        };
    }

    private static Recipient MapCustomRecipient(NotificationRecipientExt recipient) => new()
    {
        EmailAddress = recipient.EmailAddress,
        MobileNumber = recipient.MobileNumber,
        OrganizationNumber = recipient.OrganizationNumber,
        NationalIdentityNumber = recipient.NationalIdentityNumber,
        RelatedOrganizationNumber = recipient.RelatedOrganizationNumber,
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
}
