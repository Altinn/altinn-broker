using System.ComponentModel;

using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
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
            ChecksumAlgorithm = MapChecksumAlgorithm(fileTransferInitializeExt.ChecksumAlgorithm),
            DisableVirusScan = fileTransferInitializeExt.DisableVirusScan ?? false,
            Notification = MapNotification(fileTransferInitializeExt.Notification)
        };
    }

    private static ChecksumAlgorithm MapChecksumAlgorithm(ChecksumAlgorithmExt algorithm) => algorithm switch
    {
        ChecksumAlgorithmExt.SHA256 => ChecksumAlgorithm.SHA256,
        _ => ChecksumAlgorithm.MD5
    };

    private static NotificationRequest? MapNotification(NotificationRequestExt? notification)
    {
        if (notification is null)
        {
            return null;
        }

        return new NotificationRequest()
        {
            SendReminder = notification.SendReminder,
            CustomRecipients = notification.CustomRecipients?.Select(MapCustomRecipient).ToList(),
        };
    }

    private static Recipient MapCustomRecipient(NotificationRecipientExt recipient) => new()
    {
        EmailAddress = recipient.EmailAddress,
        MobileNumber = recipient.MobileNumber,
        // Custom recipient organization number - disabled for now.
        // OrganizationNumber = recipient.OrganizationNumber,
        RelatedOrganizationNumber = recipient.RelatedOrganizationNumber,
    };
}
