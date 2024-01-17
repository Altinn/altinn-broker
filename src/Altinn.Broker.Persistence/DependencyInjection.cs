using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseConnectionProvider>();
        services.AddSingleton<IActorRepository, ActorRepository>();
        services.AddSingleton<IFileRepository, FileRepository>();
        services.AddSingleton<IFileStatusRepository, FileStatusRepository>();
        services.AddSingleton<IActorFileStatusRepository, ActorFileStatusRepository>();
        services.AddSingleton<IResourceOwnerRepository, ResourceOwnerRepository>();
        services.AddSingleton<IResourceRepository, ResourceRepository>();
    }
}
