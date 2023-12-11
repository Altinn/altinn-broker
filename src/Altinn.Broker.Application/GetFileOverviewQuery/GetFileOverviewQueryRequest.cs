namespace Altinn.Broker.Application.GetFileOverviewQuery;

public class GetFileOverviewQueryRequest
{
    public Guid FileId { get; set; }
    public string Supplier { get; set; }
    public string Consumer { get; set; }

}
