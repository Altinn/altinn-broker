namespace Altinn.Broker.Application.GetAllowedRecipients;

public class GetAllowedRecipientsRequest
{
    public required string ResourceId { get; set; }

    /// <summary>The organization the end user is sending on behalf of</summary>
    public required string Party { get; set; }

    /// <summary>
    /// When true, returns every access-list party (minus the caller) without narrowing to the
    /// currently configured required party. Used by configuration UIs that need to pick a different
    /// required party than the one already set.
    /// </summary>
    public bool IgnoreRequiredParty { get; set; }
}
