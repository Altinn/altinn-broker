using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Altinn.Broker.API.IdPortenDirectAuth.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Redeems ID-Porten refresh tokens so an expired Altinn token can be re-exchanged without
/// bouncing the user through a login redirect.
///
/// ID-Porten rotates refresh tokens: redeeming one consumes it and returns a new one. The SPA
/// fires several API calls in parallel, so the same cookie can reach ValidatePrincipal on several
/// requests at once. Without coordination each of those would redeem the same refresh token and
/// all but one would fail with invalid_grant, killing the session — the very symptom this exists
/// to remove. Two guards prevent it:
///   1. an in-process single-flight lock, so concurrent requests on one replica make one call, and
///   2. a short-lived distributed cache entry keyed by the redeemed token, so requests that still
///      carry the pre-refresh cookie (or land on another replica) replay the same result.
/// </summary>
public sealed class IdPortenTokenRefreshService : IIdPortenTokenRefreshService
{
    internal const string HttpClientName = "idporten-token-refresh";

    /// <summary>
    /// How long a refresh result stays replayable for requests still carrying the pre-refresh cookie.
    /// Must comfortably outlive an in-flight request, and stay well under the refresh token lifetime.
    /// </summary>
    private static readonly TimeSpan ResultCacheLifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _singleFlightLocks = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IdPortenDirectAuthSettings> _settings;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IdPortenTokenRefreshService> _logger;

    public IdPortenTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptions<IdPortenDirectAuthSettings> settings,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
        IDistributedCache cache,
        ILogger<IdPortenTokenRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _configurationManager = configurationManager;
        _cache = cache;
        _logger = logger;
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

            var tokens = await Redeem(refreshToken, cancellationToken);
            if (tokens is null)
            {
                return null;
            }

            await CacheResult(cacheKey, tokens, cancellationToken);
            return tokens;
        }
        finally
        {
            singleFlightLock.Release();
            _singleFlightLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, singleFlightLock));
        }
    }

    private async Task<IdPortenTokens?> Redeem(string refreshToken, CancellationToken cancellationToken)
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
            return null;
        }

        if (string.IsNullOrEmpty(configuration.TokenEndpoint))
        {
            _logger.LogWarning("ID-Porten OIDC metadata has no token_endpoint; session token cannot be refreshed.");
            return null;
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
        HttpResponseMessage response;
        try
        {
            // Deliberately not retried: ID-Porten may have rotated the token before failing,
            // and a retry would then redeem a token that is already spent.
            response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ID-Porten refresh_token grant could not be sent.");
            return null;
        }

        using (response)
        {
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
                return null;
            }
        }

        return Parse(body, refreshToken);
    }

    private IdPortenTokens? Parse(string body, string redeemedRefreshToken)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var accessToken = ReadString(document.RootElement, "access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("ID-Porten refresh_token grant returned no access_token.");
                return null;
            }

            // ID-Porten rotates on every refresh; fall back to the redeemed token only for
            // providers (or stubs) that choose not to.
            var refreshToken = ReadString(document.RootElement, "refresh_token") ?? redeemedRefreshToken;
            _logger.LogInformation("Renewed the ID-Porten session; no login redirect needed.");
            return new IdPortenTokens(accessToken, refreshToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse the ID-Porten refresh_token grant response.");
            return null;
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
}
