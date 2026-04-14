namespace Altinn.Broker.Core.Repositories;

public interface IMonthlyStatisticsRepository
{
    Task<List<MonthlyResourceStatisticsData>> GetMonthlyResourceStatisticsData(
        string serviceOwnerId,
        DateTime fromInclusive,
        DateTime toExclusive,
        string? resourceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recomputes and replaces all rollup rows for the given calendar month (UTC).
    /// </summary>
    Task RebuildMonthlyStatisticsRollupForMonth(int year, int month, CancellationToken cancellationToken);
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
    public int TotalTransferDownloadAttempts { get; set; }
    public int TransfersWithDownloadConfirmed { get; set; }
}
