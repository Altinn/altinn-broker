using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IActorFileStatusRepository
{
    Task InsertActorFileStatus(
        Guid fileId,
        Domain.Enums.ActorFileStatus status,
        string actorExternalReference,
        CancellationToken ct
    );
    Task<List<ActorFileStatusEntity>> GetActorEvents(Guid fileId, CancellationToken ct);
}
