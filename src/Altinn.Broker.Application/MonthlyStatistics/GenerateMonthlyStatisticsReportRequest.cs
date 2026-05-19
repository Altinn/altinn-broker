namespace Altinn.Broker.Application.MonthlyStatistics;

public class GetMonthlyStatisticsReportRequest
{
    /// <summary>
    /// Optional resource ID to filter statistics. If omitted, all resources are included.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Year for the statistics (required).
    /// </summary>
    public required int Year { get; set; }

    /// <summary>
    /// Month for the statistics (required).
    /// </summary>
    public required int Month { get; set; }

    /// <summary>
    /// When true (default), the sender and recipient columns are populated and rows are aggregated per
    /// sender/recipient pair. When false, sender and recipient are blank and rows are aggregated only by
    /// vendor (requires <see cref="IncludeVendor"/> to also be true; otherwise the request is rejected).
    /// </summary>
    public bool IncludeEndUser { get; set; } = true;

    /// <summary>
    /// When true, the senderVendor and recipientVendor columns are populated with the organization
    /// that acts on behalf of the sender/recipient via a Maskinporten system user.
    /// Defaults to false, in which case the columns are present but empty.
    /// </summary>
    public bool IncludeVendor { get; set; } = false;
}