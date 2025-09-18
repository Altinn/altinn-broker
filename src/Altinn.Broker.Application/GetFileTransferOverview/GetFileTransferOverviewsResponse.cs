using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewsResponse
{
    public required List<FileTransferEntity> FileTransfers { get; set; }
}
