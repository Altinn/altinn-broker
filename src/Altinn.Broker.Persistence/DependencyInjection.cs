using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Repositories;

using Hangfire.PostgreSql;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseConnectionProvider>();
        services.AddSingleton<IConnectionFactory, DatabaseConnectionProvider>();
        services.AddSingleton<IActorRepository, ActorRepository>();
        services.AddSingleton<IFileTransferRepository, FileTransferRepository>();
        services.AddSingleton<IFileTransferStatusRepository, FileTransferStatusRepository>();
        services.AddSingleton<IActorFileTransferStatusRepository, ActorFileTransferStatusRepository>();
        services.AddSingleton<IServiceOwnerRepository, ServiceOwnerRepository>();
        services.AddSingleton<IIdempotencyEventRepository, IdempotencyEventRepository>();
        services.AddSingleton<IPartyRepository, PartyRepository>();
    }
}
