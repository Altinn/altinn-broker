namespace Altinn.Broker.Application.GenerateReport;

/// <summary>
/// Request parameters for generating daily summary report
/// </summary>
public class GenerateDailySummaryReportRequest
{
    /// <summary>
    /// Whether to include Altinn2 file transfers in the report.
    /// If false, only Altinn3 file transfers will be included.
    /// Default is true (include both Altinn2 and Altinn3).
    /// Note: Broker only supports Altinn3, so this parameter is kept for API compatibility but has no effect.
    /// </summary>
    public bool Altinn2Included { get; set; } = true;
}

