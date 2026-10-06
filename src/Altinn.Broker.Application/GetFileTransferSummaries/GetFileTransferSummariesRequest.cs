namespace Altinn.Broker.Application.GetFileTransferSummaries;

public class GetFileTransferSummariesRequest
{
    public required List<string> ResourceIds { get; set; }

    public string OnBehalfOf { get; set; } = string.Empty;

    public required FileTransferListView View { get; set; }
}
