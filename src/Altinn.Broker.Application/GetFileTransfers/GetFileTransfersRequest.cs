using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GetFileTransfers;

public class GetFileTransfersRequest
{
    public required string ResourceId { get; set; }
    public FileTransferStatus? Status { get; set; }
    public ActorFileTransferStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}
