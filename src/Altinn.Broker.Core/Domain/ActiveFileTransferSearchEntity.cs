using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class ActiveFileTransferSearchEntity
{
    public required ActorEntity Actor { get; set; }
    public FileTransferStatus? Status { get; set; }
    public ActorFileTransferStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public required List<string> ResourceIds { get; set; }
    public string? OrderAscending { get; set; }
    public SearchRole Role { get; set; } = SearchRole.Both;

}
