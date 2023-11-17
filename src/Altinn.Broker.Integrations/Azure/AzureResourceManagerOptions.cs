using Azure.Core;

namespace Altinn.Broker.Integrations.Azure;

public class AzureResourceManagerOptions
{
    public ResourceIdentifier SubscriptionId { get; internal set; }
    public AzureLocation Location { get; internal set; }
    public string ClientSecret { get; internal set; }
    public string ClientId { get; internal set; }
    public string TenantId { get; internal set; }
}
