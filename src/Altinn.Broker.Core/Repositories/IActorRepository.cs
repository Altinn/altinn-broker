using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IActorRepository
{
    Task AddActorAsync(Actor actor);
    Task<Actor?> GetActorAsync(long actorId);
}
