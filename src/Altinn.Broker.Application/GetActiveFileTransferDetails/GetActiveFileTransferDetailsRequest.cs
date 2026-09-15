namespace Altinn.Broker.Application.GetActiveFileTransferDetails;

public class GetActiveFileTransferDetailsRequest
{
    public required Guid FileTransferId { get; set; }
    public required string OnBehalfOf { get; set; } = string.Empty;
}