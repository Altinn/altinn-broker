using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Models.Service;

public class ResourceInitializeExt
{
    /// <summary>
    /// This should be on the form countrycode:organizationnumber. For instance 0192:922444555 for a Norwegian organization with org number 923 444 555. Corresponds to consumer.id in Maskinporten token.
    /// </summary>
    [RegularExpressionAttribute(@"^\d{4}:\d{9}$", ErrorMessage = "ResourceOwnerId should be on the Maskinporten form with countrycode:organizationnumber, for instance 0192:910753614")]
    public string OrganizationId { get; set; }

    /// <summary>
    /// ResourceId is the unique identifier for the resource as defined in the Resource Registry in Altinn Studio.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Field is only used for ad-hoc authentication. Will be removed in favour of using System User developed by Altinn Authorization, ETA Q3 2024.
    /// List of Maskinporten clients that should have access to using this service.
    /// </summary>
    public List<MaskinportenUser> PermittedMaskinportenUsers { get; set; }
}

public class MaskinportenUser
{
    public string ClientId { get; set; }
    public List<string> PermittedScopes { get; set; }
    public string OrganizationNumber { get; set; }
}
