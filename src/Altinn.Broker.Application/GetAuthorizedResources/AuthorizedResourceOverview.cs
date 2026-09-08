namespace Altinn.Broker.Application.GetAuthorizedResources;

/// <summary>
/// A broker resource the end user has access to on behalf of a party.
/// </summary>
public class AuthorizedResourceOverview
{
    public required string ResourceId { get; set; }

    /// <summary>
    /// The title of the resource in Resource Registry. Null when Resource Registry is unavailable
    /// or does not know the resource.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The name of the service owner that owns the resource, e.g. "Digitaliseringsdirektoratet".
    /// </summary>
    public string? ServiceOwnerName { get; set; }

    /// <summary>
    /// The party can initiate file transfers on the resource.
    /// </summary>
    public bool CanSend { get; set; }

    /// <summary>
    /// The party can find and download file transfers on the resource.
    /// </summary>
    public bool CanReceive { get; set; }
}
