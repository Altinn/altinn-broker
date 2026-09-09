namespace Altinn.Broker.Application.GetAuthorizedResources;

public class AuthorizedResourceOverview
{
    public required string ResourceId { get; set; }
    public string? Name { get; set; }
    public string? ServiceOwnerName { get; set; }
    public bool CanSend { get; set; }
    public bool CanReceive { get; set; }
}
