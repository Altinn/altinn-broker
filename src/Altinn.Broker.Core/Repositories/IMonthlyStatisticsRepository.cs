namespace Altinn.Broker.Core.Repositories;

public interface IMonthlyStatisticsRepository
{
    Task<List<MonthlyResourceStatisticsData>> GetMonthlyResourceStatisticsData(
        string serviceOwnerId,
        DateTime fromInclusive,
        DateTime toExclusive,
        string? resourceId,
        IReadOnlyList<string>? groupByPropertyKeys,
        CancellationToken cancellationToken);

    Task RefreshMonthlyStatisticsRollup(CancellationToken cancellationToken);
}

public class MonthlyResourceStatisticsData
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public int TotalFileTransfers { get; set; }
    public int UploadCount { get; set; }
    public int DownloadStartedCount { get; set; }
    public int UniqueDownloadStartedCount { get; set; }
    public int DownloadConfirmedCount { get; set; }
    public Dictionary<string, string> GroupedPropertyValues { get; set; } = new();
}
