namespace Altinn.Broker.Application.GetFileTransferSummaries;

/// <summary>
/// Which slice of a caller's file transfers to summarize - active (still Published),
/// historical (past Published: cancelled, purged, failed, or fully confirmed downloaded), or
/// drafts (the caller's own transfers whose file has not finished uploading).
/// </summary>
public enum FileTransferListView
{
    Active,
    Historical,
    Drafts
}
