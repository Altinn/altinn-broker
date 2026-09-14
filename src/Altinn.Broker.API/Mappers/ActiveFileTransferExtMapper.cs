using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class ActiveFileTransferExtMapper
{
    internal static ActiveFileTransferExt MapToExternalModel(FileTransferSummaryEntity summary)
    {
        return new ActiveFileTransferExt()
        {
            FileTransferId = summary.FileTransferId,
            ResourceId = summary.ResourceId,
            Sender = summary.Sender,
            Recipients = summary.Recipients,
            SendersFileTransferReference = summary.SendersFileTransferReference
        };
    }
}
