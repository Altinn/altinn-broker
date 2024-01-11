using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GetFilesQuery;

public class GetFilesQueryRequest
{
    public string Consumer { get; set; }
    public string Supplier { get; set; }
    public FileStatus? Status { get; set; }
    public ActorFileStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}
