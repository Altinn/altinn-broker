using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Common;
public static class FileTransferExtensions
{
    public static bool IsSender(this FileTransferEntity fileTransfer, string onBehalfOf)
    {
        return fileTransfer.Sender.ActorExternalId.WithoutPrefix() == onBehalfOf.WithoutPrefix();
    }

    public static bool IsRecipient(this FileTransferEntity fileTransfer, string onBehalfOf)
    {
        return fileTransfer.RecipientCurrentStatuses.Any(recipientStatus => recipientStatus.Actor.ActorExternalId.WithoutPrefix() == onBehalfOf.WithoutPrefix());
    }

    public static bool IsSenderOrRecipient(this FileTransferEntity fileTransfer, string onBehalfOf)
    {
        return fileTransfer.IsSender(onBehalfOf) || fileTransfer.IsRecipient(onBehalfOf);
    }
}
