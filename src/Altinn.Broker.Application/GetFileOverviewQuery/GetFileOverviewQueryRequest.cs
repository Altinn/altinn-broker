using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.GetFileOverviewQuery;

public class GetFileOverviewQueryRequest
{
    public MaskinportenToken Token { get; set; }
    public Guid FileId { get; set; }
}
