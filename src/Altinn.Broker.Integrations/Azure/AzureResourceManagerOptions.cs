using Azure.Core;

namespace Altinn.Broker.Integrations.Azure;

public class AzureResourceManagerOptions
{
    public required string SubscriptionId { get; set; }
    public string Location { get; set; }
    public string ClientSecret { get; set; }
    public string ClientId { get; set; }
    public string TenantId { get; set; }
}
