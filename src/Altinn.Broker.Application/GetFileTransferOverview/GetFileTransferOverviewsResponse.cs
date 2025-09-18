using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewsResponse
{
    public required IReadOnlyList<FileTransferEntity> FileTransfers { get; set; }
}
