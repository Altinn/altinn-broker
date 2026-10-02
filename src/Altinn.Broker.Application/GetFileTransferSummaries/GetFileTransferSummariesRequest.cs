namespace Altinn.Broker.Application.GetFileTransferSummaries;

public class GetFileTransferSummariesRequest
{
    public required List<string> ResourceIds { get; set; }

    public string OnBehalfOf { get; set; } = string.Empty;

    public required FileTransferListView View { get; set; }

    /// <summary>Only include file transfers that reached their current status at or after this point.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Only include file transfers that reached their current status at or before this point.</summary>
    public DateTimeOffset? To { get; set; }
}
