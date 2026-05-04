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
}