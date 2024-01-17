using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GetFilesQuery;

public class GetFilesQueryRequest
{
    public CallerIdentity Token { get; set; }
    public string ResourceId { get; set; }
    public FileStatus? Status { get; set; }
    public ActorFileStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}
