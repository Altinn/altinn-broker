using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IActorFileTransferStatusRepository
{
    Task InsertActorFileTransferStatus(
        Guid fileTransferId,
        Domain.Enums.ActorFileTransferStatus status,
        string actorExternalReference,
        string? systemVendor = null,
        CancellationToken cancellationToken = default
    );
    Task<List<ActorFileTransferStatusEntity>> GetActorEvents(Guid fileTransferId, CancellationToken cancellationToken);
}
