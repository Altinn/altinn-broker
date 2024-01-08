
namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandRequest
{
    public Guid FileId { get; set; }
    public string Supplier { get; set; }
    public Stream Filestream { get; set; }
    public string Consumer { get; set; }
    public string ClientId { get; set; }
}
