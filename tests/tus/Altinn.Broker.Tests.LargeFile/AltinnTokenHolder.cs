using Microsoft.IdentityModel.JsonWebTokens;

namespace Altinn.Broker.Tests.LargeFile;

/// <summary>
/// Caches Altinn bearer tokens and refreshes via Maskinporten exchange when expired.
/// Used for Broker API calls (e.g. file transfer overview) that do not get TUS session bypass.
/// </summary>
internal sealed class AltinnTokenHolder(HttpClient authHttpClient, AltinnAuthOptions authOptions)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetValidTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && _expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _token;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _token;
            }

            _token = await AltinnAuthClient.ExchangeAltinnTokenAsync(authHttpClient, authOptions, cancellationToken);
            _expiresAt = TryGetExpiryUtc(_token) ?? DateTimeOffset.UtcNow.AddMinutes(55);
            return _token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public Task<string> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        _expiresAt = DateTimeOffset.MinValue;
        return GetValidTokenAsync(cancellationToken);
    }

    private static DateTimeOffset? TryGetExpiryUtc(string token)
    {
        try
        {
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
            return jwt.ValidTo;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
