namespace Altinn.Broker.Core.Domain;

public class CallerIdentity
{
    public CallerIdentity(
        string scope,
        string consumer,
        string clientId
    )
    {
        Scope = scope;
        Consumer = consumer;
        ClientId = clientId;
    }

    public string Scope { get; }

    public string Consumer { get; }

    public string ClientId { get; }
}
