
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferDetailsQuery;

public class GetFileTransferDetailsQueryRequest
{
    public required CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
}
