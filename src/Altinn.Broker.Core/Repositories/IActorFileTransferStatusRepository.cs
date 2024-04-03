using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IActorFileTransferStatusRepository
{
    Task InsertActorFileTransferStatus(
        Guid fileTransferId,
        Domain.Enums.ActorFileTransferStatus status,
        string actorExternalReference,
        CancellationToken cancellationToken
    );
    Task<List<ActorFileTransferStatusEntity>> GetActorEvents(Guid fileTransferId, CancellationToken cancellationToken);
}
