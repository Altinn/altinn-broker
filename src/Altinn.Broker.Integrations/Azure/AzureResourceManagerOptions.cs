using System.ComponentModel.DataAnnotations;

using Azure.Core;

namespace Altinn.Broker.Integrations.Azure;

public class AzureResourceManagerOptions
{
    public required string SubscriptionId { get; set; }
    public string Location { get; set; }
    [StringLength(7, ErrorMessage = "The environment can only be 7 characters long because of constraint on length of Azure storage account name")]
    public string Environment { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
