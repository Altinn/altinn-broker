namespace Altinn.Broker.Integrations.Maskinporten.Models;

public class MaskinportenToken
{
    public MaskinportenToken(
        string scope,
        string consumer,
        string supplier
    )
    {
        Scope = scope;
        Consumer = consumer;
        Supplier = supplier;
    }

    public string Scope { get; }

    public string Consumer { get; }

    public string Supplier { get; }
}
