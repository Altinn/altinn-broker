namespace Altinn.Broker.Application.GenerateReport;

/// <summary>
/// Metadata about the generated report.
/// Stored in HttpContext.Items and used by the controller to add response headers.
/// </summary>
public class ReportMetadata
{
    /// <summary>
    /// Total number of records (rows) in the report
    /// </summary>
    public int TotalRecords { get; init; }

    /// <summary>
    /// Total number of file transfers across all records
    /// </summary>
    public long TotalFileTransfers { get; init; }

    /// <summary>
    /// Total number of unique service owners in the report
    /// </summary>
    public int TotalServiceOwners { get; init; }

    /// <summary>
    /// The timestamp when the report was generated (UTC)
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

