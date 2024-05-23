using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverviewQuery;

public class GetFileTransferOverviewQueryResponse
{
    public required FileTransferEntity FileTransfer { get; set; }
}
