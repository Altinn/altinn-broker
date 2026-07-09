#pragma warning disable EXTEXP0018 // HybridCache APIs are experimental in Microsoft.Extensions.Caching.Hybrid

using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Broker.Application;

public static class BrokerHybridCacheExtensions
{
    private static readonly HybridCacheEntryOptions TryGetEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
            | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    /// <summary>
    /// Read-only cache lookup. Does not write placeholder entries on cache miss.
    /// </summary>
    public static async ValueTask<string?> GetOptionalStringAsync(
        this HybridCache cache,
        string key,
        HybridCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var found = true;
        var value = await cache.GetOrCreateAsync<string?>(
            key,
            _ =>
            {
                found = false;
                return new ValueTask<string?>((string?)null);
            },
            CreateTryGetEntryOptions(options),
            cancellationToken: cancellationToken);

        return found ? value : null;
    }

    public static async ValueTask<T?> GetOptionalAsync<T>(
        this HybridCache cache,
        string key,
        HybridCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var found = true;
        var value = await cache.GetOrCreateAsync<T?>(
            key,
            _ =>
            {
                found = false;
                return new ValueTask<T?>((T?)null);
            },
            CreateTryGetEntryOptions(options),
            cancellationToken: cancellationToken);

        return found ? value : null;
    }

    private static HybridCacheEntryOptions CreateTryGetEntryOptions(HybridCacheEntryOptions? options)
    {
        // Do not copy write flags (e.g. DisableLocalCache) into read options. Writes that skip L1
        // still need reads to reach the distributed cache on miss.
        return new HybridCacheEntryOptions
        {
            Expiration = options?.Expiration,
            LocalCacheExpiration = options?.LocalCacheExpiration,
            Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
        };
    }

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

#pragma warning restore EXTEXP0018
