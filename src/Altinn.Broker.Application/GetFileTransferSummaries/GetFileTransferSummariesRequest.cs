using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GetFileTransferSummaries;

public class GetFileTransferSummariesRequest
{
    public required List<string> ResourceIds { get; set; }

    public string OnBehalfOf { get; set; } = string.Empty;

    public required FileTransferListView View { get; set; }

    /// <summary>Opaque token from the previous page. Null starts at the newest.</summary>
    public string? ContinuationToken { get; set; }

    /// <summary>Free text matched against the sender's reference.</summary>
    public string? Search { get; set; }

    /// <summary>Limit to the file transfers the party sent, or received. Default is both.</summary>
    public SearchRole Role { get; set; } = SearchRole.Both;
}
