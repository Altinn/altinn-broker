namespace Altinn.Broker.Application.GetAuthorizedResources;

public class GetAuthorizedResourcesRequest
{
    /// <summary>
    /// The party the end user acts on behalf of, as an organization number.
    /// </summary>
    public required string Party { get; set; }
}
