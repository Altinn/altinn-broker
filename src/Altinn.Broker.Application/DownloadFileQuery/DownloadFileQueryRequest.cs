
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryRequest
{
    public MaskinportenToken Token { get; set; }
    public Guid FileId { get; set; }
}
