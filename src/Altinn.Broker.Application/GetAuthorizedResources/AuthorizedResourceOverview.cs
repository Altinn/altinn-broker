namespace Altinn.Broker.Application.GetAuthorizedResources;

public class AuthorizedResourceOverview
{
    public required string ResourceId { get; set; }
    public string? Name { get; set; }
    public string? ServiceOwnerName { get; set; }
    public bool CanSend { get; set; }
    public bool CanReceive { get; set; }
    public bool CanPublish { get; set; }
    /// <summary>Whether the requested party is an organization in the Altinn service-owner catalog.</summary>
    public bool IsServiceOwner { get; set; }
    /// <summary>Whether the requested party owns this broker resource.</summary>
    public bool IsOwned { get; set; }
    /// <summary>The user has publish on digdir-broker-administrasjon for the requested party.</summary>
    public bool HasConfigureAccess { get; set; }
}
