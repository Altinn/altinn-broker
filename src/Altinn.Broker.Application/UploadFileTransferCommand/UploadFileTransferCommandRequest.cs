
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.UploadFileTransferCommand;

public class UploadFileTransferCommandRequest
{
    public Guid FileTransferId { get; set; }
    public CallerIdentity Token { get; set; }
    public Stream FileTransferStream { get; set; }
    public bool IsLegacy { get; set; }
}
