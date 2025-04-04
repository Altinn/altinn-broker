using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Options;
using Altinn.Broker.Persistence.Repositories;

using Azure.Core;
using Azure.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Altinn.Broker.Persistence;
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(BuildAzureNpgsqlDataSource(config));
        services.AddSingleton<IActorRepository, ActorRepository>();
        services.AddSingleton<IFileTransferRepository, FileTransferRepository>();
        services.AddSingleton<IFileTransferStatusRepository, FileTransferStatusRepository>();
        services.AddSingleton<IActorFileTransferStatusRepository, ActorFileTransferStatusRepository>();
        services.AddSingleton<IServiceOwnerRepository, ServiceOwnerRepository>();
        services.AddSingleton<IPartyRepository, PartyRepository>();
    }
    private static NpgsqlDataSource BuildAzureNpgsqlDataSource(IConfiguration config)
    {
        var databaseOptions = new DatabaseOptions() { ConnectionString = "" };
        config.GetSection(nameof(DatabaseOptions)).Bind(databaseOptions);
        var dataSourceBuilder = new NpgsqlDataSourceBuilder();
        dataSourceBuilder.ConnectionStringBuilder.ConnectionString = databaseOptions.ConnectionString;
        if (!string.IsNullOrWhiteSpace(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            return dataSourceBuilder.Build();
        }

        var psqlServerTokenProvider = new DefaultAzureCredential();
        var tokenRequestContext = new TokenRequestContext(scopes: ["https://ossrdbms-aad.database.windows.net/.default"]) { };
        dataSourceBuilder.UsePeriodicPasswordProvider(async (_, cancellationToken) => 
            psqlServerTokenProvider.GetTokenAsync(tokenRequestContext).Result.Token, TimeSpan.FromMinutes(45), TimeSpan.FromSeconds(0)
        );

        
        return dataSourceBuilder.Build();
    }
}
