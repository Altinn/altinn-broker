using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IActorRepository
{
    Task<long> AddActorAsync(ActorEntity actor);
    Task<ActorEntity?> GetActorAsync(string actorReference);
}