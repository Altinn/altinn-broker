namespace Altinn.Broker.Application.MonthlyStatistics;

public class GetMonthlyStatisticsCsvResponse
{
    public required byte[] Content { get; set; }
    public required string FileName { get; set; }
    public int RowCount { get; set; }
}
