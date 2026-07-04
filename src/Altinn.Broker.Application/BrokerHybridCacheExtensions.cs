using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Broker.Application;

public static class BrokerHybridCacheExtensions
{
    public static ValueTask<string?> GetOptionalStringAsync(
        this HybridCache cache,
        string key,
        HybridCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync<string?>(
            key,
            static _ => new ValueTask<string?>((string?)null),
            options,
            cancellationToken: cancellationToken);

    public static ValueTask<T?> GetOptionalAsync<T>(
        this HybridCache cache,
        string key,
        HybridCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : class
        => cache.GetOrCreateAsync<T?>(
            key,
            static _ => new ValueTask<T?>((T?)null),
            options,
            cancellationToken: cancellationToken);

    public static Task SetStringAsync(
        this HybridCache cache,
        string key,
        string value,
        HybridCacheEntryOptions options,
        CancellationToken cancellationToken = default)
        => cache.SetAsync(key, value, options, cancellationToken: cancellationToken).AsTask();

    public static Task RemoveKeyAsync(
        this HybridCache cache,
        string key,
        CancellationToken cancellationToken = default)
        => cache.RemoveAsync(key, cancellationToken).AsTask();
}
