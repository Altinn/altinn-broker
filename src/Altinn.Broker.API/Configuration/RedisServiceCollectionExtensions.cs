using Microsoft.Extensions.Caching.StackExchangeRedis;

using StackExchange.Redis;

namespace Altinn.Broker.API.Configuration;

public static class RedisServiceCollectionExtensions
{
    public static string? GetRedisConnectionString(IConfiguration configuration)
    {
        var fromSection = configuration
            .GetSection(DistributedCacheOptions.SectionName)
            .Get<DistributedCacheOptions>()
            ?.RedisConnectionString;

        var fromEnv = configuration["DistributedCacheOptions:RedisConnectionString"];
        var connectionString = (fromSection ?? fromEnv)?.Trim();
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    public static void AddBrokerDistributedCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = GetRedisConnectionString(configuration);
        if (redisConnectionString is null)
        {
            services.AddDistributedMemoryCache();
            return;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
        });

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 10_000;
            options.ConnectRetry = 3;

            var logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Redis");

            var multiplexer = ConnectionMultiplexer.Connect(options);
            multiplexer.ConnectionFailed += (_, args) =>
                logger.LogError(args.Exception, "Redis connection failed ({FailureType})", args.FailureType);
            multiplexer.ConnectionRestored += (_, _) =>
                logger.LogInformation("Redis connection restored");

            if (multiplexer.IsConnected)
            {
                logger.LogInformation(
                    "Redis connected to {Endpoints}",
                    string.Join(", ", multiplexer.GetEndPoints().Select(endpoint => endpoint.ToString())));
            }
            else
            {
                logger.LogWarning(
                    "Redis multiplexer created for {Endpoints} but not connected yet. " +
                    "Check Azure Cache firewall rules allow traffic from this Container App.",
                    string.Join(", ", multiplexer.GetEndPoints().Select(endpoint => endpoint.ToString())));
            }

            return multiplexer;
        });
    }

    public static void LogDistributedCacheStatus(this WebApplication app)
    {
        var redisConnectionString = GetRedisConnectionString(app.Configuration);
        if (redisConnectionString is null)
        {
            app.Logger.LogWarning(
                "Distributed cache is in-memory. Set {EnvVar} for multi-replica TUS coordination.",
                "DistributedCacheOptions__RedisConnectionString");
            return;
        }

        var endpoints = ConfigurationOptions.Parse(redisConnectionString).EndPoints;
        app.Logger.LogInformation(
            "Distributed cache: Redis configured ({EndpointCount} endpoint(s): {Endpoints})",
            endpoints.Count,
            string.Join(", ", endpoints.Select(endpoint => endpoint.ToString())));

        var multiplexer = app.Services.GetService<IConnectionMultiplexer>();
        if (multiplexer is null)
        {
            app.Logger.LogError(
                "Redis connection string is set but {Service} is not registered in DI.",
                nameof(IConnectionMultiplexer));
            return;
        }

        app.Logger.LogInformation(
            "Redis multiplexer state: {IsConnected}",
            multiplexer.IsConnected ? "connected" : "not connected");
    }
}
