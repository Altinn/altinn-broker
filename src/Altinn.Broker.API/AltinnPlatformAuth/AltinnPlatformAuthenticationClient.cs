using Altinn.Broker.Core.Options;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.AltinnPlatformAuth;

public sealed class AltinnPlatformAuthenticationClient : IAltinnPlatformAuthenticationClient
{
    private readonly HttpClient _httpClient;
    private readonly AltinnOptions _altinnOptions;
    private readonly ILogger<AltinnPlatformAuthenticationClient> _logger;

    public AltinnPlatformAuthenticationClient(
        HttpClient httpClient,
        IOptions<AltinnOptions> altinnOptions,
        ILogger<AltinnPlatformAuthenticationClient> logger)
    {
        _httpClient = httpClient;
        _altinnOptions = altinnOptions.Value;
        _logger = logger;
    }

    public async Task<string?> RefreshTokenAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var refreshUrl = $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/refresh";

        using var request = new HttpRequestMessage(HttpMethod.Get, refreshUrl);

        if (httpContext.Request.Headers.TryGetValue("Cookie", out var cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader.ToString());
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
                "Altinn platform token refresh failed. Status={StatusCode} Url={Url} Body={Body}",
                (int)response.StatusCode,
                refreshUrl,
                Truncate(body, 300));
            return null;
        }

        var token = body.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength] + "…";
    }
}
