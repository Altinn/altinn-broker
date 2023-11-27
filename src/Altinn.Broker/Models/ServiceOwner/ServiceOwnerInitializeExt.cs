using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Models.ServiceOwner;

public class ServiceOwnerInitializeExt
{
    public ServiceOwnerInitializeExt() { }

    /// <summary>
    /// This should be on the form countrycode:organizationnumber. For instance 0192:922444555 for a Norwegian organization with org number 923 444 555. Corresponds to consumer.id in Maskinporten token.
    /// </summary>
    [RegularExpressionAttribute(@"^\d{4}:\d{9}$", ErrorMessage = "ServiceOwnerId should be on the Maskinporten form with countrycode:organizationnumber, for instance 0192:910753614")]
    public string Id { get; set; }

    public string Name { get; set; }
}
