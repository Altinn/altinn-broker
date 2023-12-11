
namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryRequest
{
    public Guid FileId { get; set; }
    public string Supplier { get; set; }
    public string Consumer { get; set; }
}
