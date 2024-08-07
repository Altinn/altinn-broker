
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferDetails;

public class GetFileTransferDetailsRequest
{
    public required CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
}
