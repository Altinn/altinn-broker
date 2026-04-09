namespace Altinn.Broker.Core.Options;

public class ReportFilterOptions
{
    /// <summary>
    /// Comma-separated list of resource IDs for which file transfers with
    /// statusMessage=true should be excluded from the daily summary report.
    /// When null or empty, no filtering is applied.
    /// Sourced from an Azure Key Vault secret.
    /// </summary>
    public string? ReportResourceIdFilter { get; set; }
}
