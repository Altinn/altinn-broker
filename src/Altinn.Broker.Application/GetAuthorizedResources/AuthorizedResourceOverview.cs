namespace Altinn.Broker.Application.GetAuthorizedResources;

public class AuthorizedResourceOverview
{
    public required string ResourceId { get; set; }
    public string? Name { get; set; }
    public string? ServiceOwnerName { get; set; }
    public bool CanSend { get; set; }
    public bool CanReceive { get; set; }
    public bool CanPublish { get; set; }
    /// <summary>Whether the requested party is configured as a Broker service owner.</summary>
    public bool IsServiceOwner { get; set; }
}
