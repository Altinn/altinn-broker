using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewRequest
{
    public required CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
}
