
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandRequest
{
    public Guid FileId { get; set; }
    public CallerIdentity Token { get; set; }
    public Stream Filestream { get; set; }
}
