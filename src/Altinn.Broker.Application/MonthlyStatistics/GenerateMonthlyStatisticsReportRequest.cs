namespace Altinn.Broker.Application.MonthlyStatistics;

public class GetMonthlyStatisticsReportRequest
{
    public string? ResourceId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}