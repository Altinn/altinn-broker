
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileTransferDetailsQuery;

public class GetFileTransferDetailsQueryRequest
{
    public CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
}
