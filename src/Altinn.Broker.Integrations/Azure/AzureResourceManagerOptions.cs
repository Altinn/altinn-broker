using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Integrations.Azure;

public class AzureResourceManagerOptions
{
    public string Location { get; set; } = string.Empty;
    [StringLength(7, ErrorMessage = "The environment can only be 7 characters long because of constraint on length of Azure storage account name")]
    public string Environment { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ApplicationResourceGroupName { get; set; } = string.Empty;
    public string MalwareScanEventGridTopicName { get; set; } = string.Empty;
}
