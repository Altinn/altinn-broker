using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FrontendFileTransferSearchEntity
{
    public required ActorEntity Actor { get; set; }

    /// <summary>Statuses to match when a file transfer matched the actor as its sender. Null/empty means no filter.</summary>
    public List<FileTransferStatus>? SenderStatuses { get; set; }

    /// <summary>Statuses to match when a file transfer matched the actor as a recipient. Null/empty means no filter.</summary>
    public List<FileTransferStatus>? RecipientStatuses { get; set; }
    public ActorFileTransferStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public required List<string> ResourceIds { get; set; }
    public string? OrderAscending { get; set; }
    public SearchRole Role { get; set; } = SearchRole.Both;

}
