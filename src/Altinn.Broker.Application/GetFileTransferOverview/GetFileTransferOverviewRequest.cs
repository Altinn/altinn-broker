using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewRequest
{
    public List<Guid>? FileTransferIds { get; set; }
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
    public string? OnBehalfOfConsumer { get; set; }
}
