using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class FileTransferSummaryExtMapper
{
    internal static FileTransferSummaryExt MapToExternalModel(FileTransferSummaryEntity summary)
    {
        return new FileTransferSummaryExt()
        {
            FileTransferId = summary.FileTransferId,
            ResourceId = summary.ResourceId,
            Sender = summary.Sender,
            Recipients = summary.Recipients,
            SendersFileTransferReference = summary.SendersFileTransferReference
        };
    }
}
