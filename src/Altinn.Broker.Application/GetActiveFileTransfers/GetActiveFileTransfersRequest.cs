namespace Altinn.Broker.Application.GetActiveFileTransfers;

public class GetActiveFileTransfersRequest
{
    public required List<string> ResourceIds { get; set; }

    public string OnBehalfOf { get; set; } = string.Empty;
}
