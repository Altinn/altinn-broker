namespace Altinn.Broker.Application.GetFileTransferSummaries;

/// <summary>
/// Which slice of a caller's file transfers to summarize - active (still Published) or
/// historical (past Published: cancelled, purged, failed, or fully confirmed downloaded).
/// </summary>
public enum FileTransferListView
{
    Active,
    Historical
}
