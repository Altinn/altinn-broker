namespace Altinn.Broker.Models.ServiceOwner;

public class ServiceOwnerInitializeExt
{
    public ServiceOwnerInitializeExt() { }

    /// <summary>
    /// This should be on the form countrycode:organizationnumber. For instance 0192:922444555 for a Norwegian organization with org number 923 444 555. Corresponds to consumer.id in Maskinporten token.
    /// </summary>
    public string Id { get; set; }

    public string Name { get; set; }

    public string ResourceGroupName { get; set; }  
    
    public string StorageAccountName { get; set; }
}
