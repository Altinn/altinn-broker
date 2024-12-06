
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.UploadFile;

public class UploadFileRequest
{
    public Guid FileTransferId { get; set; }
    public required Stream UploadStream { get; set; }
    public bool IsLegacy { get; set; }
    public long ContentLength { get; set; }
    public string? OnBehalfOfConsumer { get; set; }
}
