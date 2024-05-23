using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GetFileTransfersQuery;

public class LegacyGetFilesQueryRequest
{
    public required CallerIdentity Token { get; set; }
    public string? ResourceId { get; set; }
    public FileTransferStatus? FileTransferStatus { get; set; }
    public ActorFileTransferStatus? RecipientFileTransferStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string[]? Recipients { get; set; }
    public string? OnBehalfOfConsumer { get; set; }
}
