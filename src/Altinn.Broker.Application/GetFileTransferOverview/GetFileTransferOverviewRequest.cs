using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewRequest
{
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
    public string? OnBehalfOf { get; set; }
}
