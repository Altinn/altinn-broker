using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

using StackExchange.Redis;

namespace Altinn.Broker.Tests.Tus;

internal static class TestHybridCacheFactory
{
    public static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    public static TusUploadProgressCache CreateProgressCache(IConnectionMultiplexer? redis = null)
        => new(
            CreateHybridCache(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TusUploadProgressCache>.Instance,
            redis);

    public static TusPartialUploadRegistry CreatePartialUploadRegistry()
        => new(CreateHybridCache());
}
