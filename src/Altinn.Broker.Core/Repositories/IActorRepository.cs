using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IActorRepository
{
    Task<long> AddActorAsync(Actor actor);
    Task<Actor?> GetActorAsync(long actorId);
}