namespace Altinn.Broker.Application.MonthlyStatistics;

public class MonthlyStatisticsRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public int UploadCount { get; set; }
    public int DownloadStartedCount { get; set; }
    public int UniqueDownloadStartedCount { get; set; }
    public int DownloadConfirmedCount { get; set; }
    public Dictionary<string, string> GroupedPropertyValues { get; set; } = new();
}
