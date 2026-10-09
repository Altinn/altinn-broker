using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Altinn.Broker.API.IdPortenDirectAuth.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using StackExchange.Redis;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Redeems ID-Porten refresh tokens so an expired Altinn token can be re-exchanged without a
/// login redirect. ID-Porten consumes the token it is given and returns a new one, and the SPA
/// calls the API in parallel across Container App replicas, so redemption is gated by an
/// in-process single-flight lock, a Redis lock (when available), and a short-lived distributed
/// cache keyed on the redeemed token. Without that, competing redeems fail with invalid_grant
/// and end the session.
/// </summary>
public sealed class IdPortenTokenRefreshService : IIdPortenTokenRefreshService
{
    internal const string HttpClientName = "idporten-token-refresh";

    /// <summary>
    /// How long a result stays replayable for requests still carrying the old cookie. Longer than
    /// the Altinn token lifetime so a stale Set-Cookie that rewrites a spent refresh token can
    /// still recover via cache.
    /// </summary>
    private static readonly TimeSpan ResultCacheLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Must cover a full ID-Porten round-trip on the winning replica (HTTP client timeout is 10s).
    /// </summary>
    private static readonly TimeSpan DefaultConcurrentRefreshWait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan DefaultConcurrentRefreshPollInterval = TimeSpan.FromMilliseconds(50);

    private static readonly TimeSpan DistributedLockLifetime = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _singleFlightLocks = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IdPortenDirectAuthSettings> _settings;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<IdPortenTokenRefreshService> _logger;
    private readonly TimeSpan _concurrentRefreshWait;
    private readonly TimeSpan _concurrentRefreshPollInterval;

    public IdPortenTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptions<IdPortenDirectAuthSettings> settings,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
        IDistributedCache cache,
        ILogger<IdPortenTokenRefreshService> logger,
        IConnectionMultiplexer? redis = null)
        : this(
            httpClientFactory,
            settings,
            configurationManager,
            cache,
            logger,
            DefaultConcurrentRefreshWait,
            DefaultConcurrentRefreshPollInterval,
            redis)
    {
    }

    internal IdPortenTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptions<IdPortenDirectAuthSettings> settings,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
        IDistributedCache cache,
        ILogger<IdPortenTokenRefreshService> logger,
        TimeSpan concurrentRefreshWait,
        TimeSpan concurrentRefreshPollInterval,
        IConnectionMultiplexer? redis = null)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _configurationManager = configurationManager;
        _cache = cache;
        _logger = logger;
        _concurrentRefreshWait = concurrentRefreshWait;
        _concurrentRefreshPollInterval = concurrentRefreshPollInterval;
        _redis = redis;
    }

    public Task<IdPortenTokens?> GetCachedRotationAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Task.FromResult<IdPortenTokens?>(null);
        }

        return ReadCachedResult(CacheKey(refreshToken), cancellationToken);
    }

    public async Task<IdPortenTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        var cacheKey = CacheKey(refreshToken);

        var cached = await ReadCachedResult(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var singleFlightLock = _singleFlightLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await singleFlightLock.WaitAsync(cancellationToken);
        try
        {
            cached = await ReadCachedResult(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }

            var distributedLock = await TryAcquireDistributedLock(cacheKey, cancellationToken);
            if (distributedLock is null)
            {
                // Another replica is redeeming. Never call ID-Porten with a token that may already
                // be spent — wait for that replica to publish the rotated tokens.
                return await WaitForCachedResult(cacheKey, cancellationToken);
            }

            try
            {
                cached = await ReadCachedResult(cacheKey, cancellationToken);
                if (cached is not null)
                {
                    return cached;
                }

                var redeemResult = await Redeem(refreshToken, cancellationToken);
                if (redeemResult.Tokens is not null)
                {
                    await CacheResult(cacheKey, redeemResult.Tokens, cancellationToken);
                    return redeemResult.Tokens;
                }

                // Metadata/config failed before the token was sent — safe to retry once after
                // releasing the lock so another replica is not blocked on a dead holder.
                if (redeemResult.FailureKind == RedeemFailureKind.PreRequest)
                {
                    await distributedLock.DisposeAsync();
                    distributedLock = null;

                    return await RetryRedeemAfterPreRequestFailureAsync(
                        refreshToken,
                        cacheKey,
                        cancellationToken);
                }

                // Ambiguous: the refresh token may already have been spent. Do not redeem again.
                return await WaitForCachedResult(cacheKey, cancellationToken);
            }
            finally
            {
                if (distributedLock is not null)
                {
                    await distributedLock.DisposeAsync();
                }
            }
        }
        finally
        {
            singleFlightLock.Release();
            _singleFlightLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, singleFlightLock));
        }
    }

    private async Task<IdPortenTokens?> RetryRedeemAfterPreRequestFailureAsync(
        string refreshToken,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var cached = await ReadCachedResult(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        await using var retryLock = await TryAcquireDistributedLock(cacheKey, cancellationToken);
        if (retryLock is null)
        {
            return await WaitForCachedResult(cacheKey, cancellationToken);
        }

        cached = await ReadCachedResult(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var redeemResult = await Redeem(refreshToken, cancellationToken);
        if (redeemResult.Tokens is not null)
        {
            await CacheResult(cacheKey, redeemResult.Tokens, cancellationToken);
            return redeemResult.Tokens;
        }

        if (redeemResult.FailureKind == RedeemFailureKind.Ambiguous)
        {
            return await WaitForCachedResult(cacheKey, cancellationToken);
        }

        return null;
    }

    private async Task<IAsyncDisposable?> TryAcquireDistributedLock(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        if (_redis is null)
        {
            return NoOpAsyncDisposable.Instance;
        }

        try
        {
            var db = _redis.GetDatabase();
            var lockKey = $"lock:{cacheKey}";
            var lockValue = Guid.NewGuid().ToString("N");
            var acquired = await db.StringSetAsync(
                lockKey,
                lockValue,
                DistributedLockLifetime,
                When.NotExists);
            if (!acquired)
            {
                return null;
            }

            return new RedisLock(db, lockKey, lockValue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not acquire the ID-Porten refresh lock; falling back to local single-flight.");
            return NoOpAsyncDisposable.Instance;
        }
    }

    private async Task<IdPortenTokens?> WaitForCachedResult(string cacheKey, CancellationToken cancellationToken)
    {
        var cached = await ReadCachedResult(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogInformation(
                "Recovered ID-Porten tokens from cache after a concurrent refresh race.");
            return cached;
        }

        var deadline = DateTimeOffset.UtcNow + _concurrentRefreshWait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(_concurrentRefreshPollInterval, cancellationToken);
            cached = await ReadCachedResult(cacheKey, cancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation(
                    "Recovered ID-Porten tokens from cache after a concurrent refresh race.");
                return cached;
            }
        }

        _logger.LogWarning(
            "Timed out waiting for a concurrent ID-Porten refresh to publish rotated tokens.");
        return null;
    }

    private enum RedeemFailureKind
    {
        None,
        /// <summary>Failed before the refresh token was sent; redeeming again is safe.</summary>
        PreRequest,
        /// <summary>Failed after dispatch; the token may already be spent.</summary>
        Ambiguous
    }

    private readonly record struct RedeemResult(IdPortenTokens? Tokens, RedeemFailureKind FailureKind)
    {
        public static RedeemResult Ok(IdPortenTokens tokens) => new(tokens, RedeemFailureKind.None);

        public static RedeemResult PreRequest() => new(null, RedeemFailureKind.PreRequest);

        public static RedeemResult Ambiguous() => new(null, RedeemFailureKind.Ambiguous);
    }

    private async Task<RedeemResult> Redeem(string refreshToken, CancellationToken cancellationToken)
    {
        var settings = _settings.Value;

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read ID-Porten OIDC metadata; session token cannot be refreshed.");
            return RedeemResult.PreRequest();
        }

        if (string.IsNullOrEmpty(configuration.TokenEndpoint))
        {
            _logger.LogWarning("ID-Porten OIDC metadata has no token_endpoint; session token cannot be refreshed.");
            return RedeemResult.PreRequest();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret
            })
        };

        string body;
        try
        {
            // Ambiguous on failure: ID-Porten may have rotated the token before the response arrived.
            using var response = await _httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ID-Porten refresh_token grant failed. Status={StatusCode} Error={Error}. " +
                    "Common causes: the refresh token has already been redeemed (ID-Porten rotates them), " +
                    "refresh_token_lifetime expired before renewal was needed, the client is not registered " +
                    "with refresh_token as a grant type, or the session was revoked.",
                    (int)response.StatusCode,
                    ReadErrorCode(body));
                return RedeemResult.Ambiguous();
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Client timeouts surface as TaskCanceledException. This runs on every request's auth
            // path, so it must end the session cleanly rather than throw a 500.
            _logger.LogWarning(ex, "ID-Porten refresh_token grant failed to complete.");
            return RedeemResult.Ambiguous();
        }

        return Parse(body, refreshToken);
    }

    private RedeemResult Parse(string body, string redeemedRefreshToken)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var accessToken = ReadString(document.RootElement, "access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("ID-Porten refresh_token grant returned no access_token.");
                return RedeemResult.Ambiguous();
            }

            // ID-Porten rotates on every refresh; the fallback covers providers that do not.
            var refreshToken = ReadString(document.RootElement, "refresh_token") ?? redeemedRefreshToken;
            _logger.LogInformation("Renewed the ID-Porten session; no login redirect needed.");
            return RedeemResult.Ok(new IdPortenTokens(accessToken, refreshToken));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse the ID-Porten refresh_token grant response.");
            return RedeemResult.Ambiguous();
        }
    }

    private async Task<IdPortenTokens?> ReadCachedResult(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (cached is null)
            {
                return null;
            }

            _logger.LogDebug("Replayed a recent ID-Porten refresh instead of redeeming a spent token.");
            return JsonSerializer.Deserialize<IdPortenTokens>(cached);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A cache miss is always safe to recover from; a cache outage must not log the user out.
            _logger.LogWarning(ex, "Could not read the cached ID-Porten refresh result.");
            return null;
        }
    }

    private async Task CacheResult(string cacheKey, IdPortenTokens tokens, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(tokens),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ResultCacheLifetime },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not cache the ID-Porten refresh result; parallel requests may need to re-login.");
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string ReadErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadString(document.RootElement, "error") ?? "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }

    /// <summary>Refresh tokens are credentials, so only their hash reaches the cache key.</summary>
    private static string CacheKey(string refreshToken) =>
        $"idporten-refresh:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)))}";

    private sealed class RedisLock(IDatabase db, string key, string value) : IAsyncDisposable
    {
        private const string ReleaseIfOwnerScript =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.ScriptEvaluateAsync(
                    ReleaseIfOwnerScript,
                    keys: [key],
                    values: [value]);
            }
            catch
            {
                // Lock TTL is the safety net if release fails.
            }
        }
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoOpAsyncDisposable Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
