namespace Altinn.Broker.Application.GetAllowedRecipients;

public class GetAllowedRecipientsRequest
{
    public required string ResourceId { get; set; }

    /// <summary>The organization the end user is sending on behalf of</summary>
    public required string Party { get; set; }
}
