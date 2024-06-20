
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandRequest
{
    public Guid FileTransferId { get; set; }
    public required CallerIdentity Token { get; set; }
    public required Stream UploadStream { get; set; }
    public bool IsLegacy { get; set; }
    public long ContentLength { get; set; }
}
