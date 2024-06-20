using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverviewQuery;

public class GetFileTransferOverviewQueryRequest
{
    public required CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
}
