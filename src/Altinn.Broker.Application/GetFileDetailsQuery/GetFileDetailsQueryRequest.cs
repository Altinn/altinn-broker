
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryRequest
{
    public MaskinportenToken Token { get; set; }
    public Guid FileId { get; set; }
}
