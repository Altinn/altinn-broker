namespace Altinn.Broker.Core.Domain;

public class MaskinportenToken
{
    public MaskinportenToken(
        string scope,
        string consumer,
        string supplier,
        string clientId
    )
    {
        Scope = scope;
        Consumer = consumer;
        Supplier = supplier;
        ClientId = clientId;
    }

    public string Scope { get; }

    public string Consumer { get; }

    public string Supplier { get; }

    public string ClientId { get; }
}
