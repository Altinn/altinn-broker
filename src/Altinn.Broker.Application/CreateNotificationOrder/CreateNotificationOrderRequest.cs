using Altinn.Broker.Application.InitializeFileTransfer;

namespace Altinn.Broker.Application.CreateNotificationOrder;

public class CreateNotificationOrderRequest
{
    public required NotificationRequest NotificationRequest { get; set; }
    public required string SendersFileTransferReference { get; set; }
    public required Guid FileTransferId { get; set; }
    public required string ResourceId { get; set; }
    public required string SenderExternalId { get; set; }
    public required string FileName { get; set; }
    public required List<string> RecipientExternalIds { get; set; }
    public DateTime FileTransferExpirationTime { get; set; }
}
