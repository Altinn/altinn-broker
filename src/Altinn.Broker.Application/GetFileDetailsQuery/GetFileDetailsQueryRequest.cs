
namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryRequest
{
    public Guid FileId { get; set; }
    public string Supplier { get; set; }
    public string Consumer { get; set; }
    public string ClientId { get; set; }
}
