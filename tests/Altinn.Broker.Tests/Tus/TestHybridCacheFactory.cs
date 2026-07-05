using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

using StackExchange.Redis;

namespace Altinn.Broker.Tests.Tus;

internal sealed class HybridCacheTestScope : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public HybridCache Cache { get; }

    private HybridCacheTestScope(ServiceProvider provider, HybridCache cache)
    {
        _provider = provider;
        Cache = cache;
    }

    public static HybridCacheTestScope Create()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return new HybridCacheTestScope(provider, provider.GetRequiredService<HybridCache>());
    }

    public TusPartialUploadRegistry CreatePartialUploadRegistry()
        => new(_provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>());

    public TusUploadProgressCache CreateProgressCache(IConnectionMultiplexer? redis = null)
        => new(
            Cache,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TusUploadProgressCache>.Instance,
            redis);

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
}

internal static class TestHybridCacheFactory
{
    public static HybridCacheTestScope CreateScope() => HybridCacheTestScope.Create();
}
