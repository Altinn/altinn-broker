using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Altinn.Broker.Tests.LargeFile;

/// <summary>
/// Fetches and caches Altinn test tokens, refreshing before JWT expiry.
/// </summary>
public sealed class AccessTokenProvider(Func<CancellationToken, Task<string>> fetchTokenAsync)
{
    private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null && _expiresAt > DateTimeOffset.UtcNow.Add(RefreshBeforeExpiry))
        {
            return _token;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _expiresAt > DateTimeOffset.UtcNow.Add(RefreshBeforeExpiry))
            {
                return _token;
            }

            _token = await fetchTokenAsync(cancellationToken);
            _expiresAt = ReadJwtExpiry(_token);
            return _token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            _token = await fetchTokenAsync(cancellationToken);
            _expiresAt = ReadJwtExpiry(_token);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void ApplyAuthorization(HttpRequestMessage request, string token)
        => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static DateTimeOffset ReadJwtExpiry(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return DateTimeOffset.UtcNow.AddMinutes(30);
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.TryGetProperty("exp", out var expElement)
            && expElement.TryGetInt64(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return DateTimeOffset.UtcNow.AddMinutes(30);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}
