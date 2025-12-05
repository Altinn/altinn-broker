using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewResponse
{
    public required FileTransferEntity FileTransfer { get; set; }
    public required List<FileTransferStatusEntity> FileTransferEvents { get; set; }

}
