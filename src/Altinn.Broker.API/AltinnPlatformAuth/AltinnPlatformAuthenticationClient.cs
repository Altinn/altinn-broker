using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.Core.Options;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.AltinnPlatformAuth;

public sealed class AltinnPlatformAuthenticationClient : IAltinnPlatformAuthenticationClient
{
    private readonly HttpClient _httpClient;
    private readonly AltinnOptions _altinnOptions;
    private readonly AltinnPlatformAuthSettings _platformAuthSettings;
    private readonly ILogger<AltinnPlatformAuthenticationClient> _logger;

    public AltinnPlatformAuthenticationClient(
        HttpClient httpClient,
        IOptions<AltinnOptions> altinnOptions,
        IOptions<AltinnPlatformAuthSettings> platformAuthSettings,
        ILogger<AltinnPlatformAuthenticationClient> logger)
    {
        _httpClient = httpClient;
        _altinnOptions = altinnOptions.Value;
        _platformAuthSettings = platformAuthSettings.Value;
        _logger = logger;
    }

    public async Task<string?> RefreshTokenAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var refreshUrl = $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/refresh";

        using var request = new HttpRequestMessage(HttpMethod.Get, refreshUrl);

        if (TryGetNamedCookie(httpContext, _platformAuthSettings.JwtCookieName, out var cookieValue))
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"{_platformAuthSettings.JwtCookieName}={cookieValue}");
        }

        if (!string.IsNullOrEmpty(_altinnOptions.PlatformSubscriptionKey))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", _altinnOptions.PlatformSubscriptionKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Altinn platform token refresh failed. Status={StatusCode} Url={Url}",
                (int)response.StatusCode,
                refreshUrl);
            return null;
        }

        var token = body.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static bool TryGetNamedCookie(HttpContext httpContext, string cookieName, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(cookieName))
        {
            return false;
        }

        if (httpContext.Request.Cookies.TryGetValue(cookieName, out var fromCollection)
            && !string.IsNullOrEmpty(fromCollection))
        {
            value = fromCollection;
            return true;
        }

        if (!httpContext.Request.Headers.TryGetValue("Cookie", out var cookieHeader))
        {
            return false;
        }

        foreach (var part in cookieHeader.ToString().Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = part[..separator].Trim();
            if (!string.Equals(name, cookieName, StringComparison.Ordinal))
            {
                continue;
            }

            value = part[(separator + 1)..].Trim();
            return !string.IsNullOrEmpty(value);
        }

        return false;
    }
}
