namespace Altinn.Broker.Application.MonthlyStatistics;

public class GenerateMonthlyStatisticsReportRequest
{
    public string? ResourceId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}