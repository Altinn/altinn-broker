namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Represents aggregated daily summary data for file transfers from the database
/// </summary>
public class DailySummaryData
{
    public required DateTimeOffset Date { get; set; }
    public required string ServiceOwnerId { get; set; }
    public required string ServiceOwnerName { get; set; }
    public required string ResourceId { get; set; }
    public required int FileTransferCount { get; set; }
}

