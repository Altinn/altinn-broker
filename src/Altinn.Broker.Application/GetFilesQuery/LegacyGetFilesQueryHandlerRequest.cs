using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GetFilesQuery;

public class LegacyGetFilesQueryRequest
{
    public CallerIdentity Token { get; set; }
    public string? ResourceId { get; set; }
    public ActorFileStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string[]? Recipients { get; set; }
    public string? OnBehalfOfConsumer { get; set; }
}
