
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandRequest
{
    public Guid FileTransferId { get; set; }
    public CallerIdentity Token { get; set; }
    public Stream UploadStream { get; set; }
    public bool IsLegacy { get; set; }
}
