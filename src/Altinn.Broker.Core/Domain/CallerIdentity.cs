namespace Altinn.Broker.Core.Domain;

public class CallerIdentity
{
    public CallerIdentity(
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
