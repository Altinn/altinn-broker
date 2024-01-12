using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileSearchEntity
{
    public ActorEntity Actor { get; set; }
    public FileStatus? Status { get; set; }
    public ActorFileStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}
