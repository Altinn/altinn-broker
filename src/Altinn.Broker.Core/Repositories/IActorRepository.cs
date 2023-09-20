using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IActorRepository
{
    void AddActor(Actor actor);
    Actor? GetActor(long actorId);
}