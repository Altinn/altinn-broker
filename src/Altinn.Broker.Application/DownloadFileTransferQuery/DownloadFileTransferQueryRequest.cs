
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.DownloadFileTransferQuery;
public class DownloadFileTransferQueryRequest
{
    public CallerIdentity Token { get; set; }
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
}
