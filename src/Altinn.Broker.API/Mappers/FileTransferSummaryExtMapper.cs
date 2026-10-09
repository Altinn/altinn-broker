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
            IsSender = summary.IsSender,
            Recipients = summary.Recipients,
            SendersFileTransferReference = summary.SendersFileTransferReference,
            ExpirationTime = summary.ExpirationTime
        };
    }

    internal static FileTransferSummaryListExt MapToExternalModel(FileTransferSummaryPage page)
    {
        return new FileTransferSummaryListExt()
        {
            Items = page.Summaries.Select(MapToExternalModel).ToList(),
            HasNextPage = page.HasNextPage,
            ContinuationToken = page.ContinuationToken
        };
    }
}
