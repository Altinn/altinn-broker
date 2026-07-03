using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.DownloadFile;
public class DownloadFileRequest
{
    public Guid FileTransferId { get; set; }
    public bool IsLegacy { get; set; }
    public string? OnBehalfOfConsumer { get; set; }
    public ByteRangeRequest? Range { get; set; }
}
