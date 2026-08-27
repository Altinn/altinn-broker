using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Altinn.Broker.Core.Options;

namespace Altinn.Broker.Integrations.Altinn;

public interface IAltinnTokenExchangeService
{
    Task<string?> ExchangeIdPortenToken(string idPortenAccessToken, CancellationToken cancellationToken = default);
}

public class AltinnTokenExchangeService : IAltinnTokenExchangeService
{
    private readonly HttpClient _httpClient;
    private readonly AltinnOptions _altinnOptions;
    private readonly ILogger<AltinnTokenExchangeService> _logger;

    public AltinnTokenExchangeService(
        HttpClient httpClient,
        IOptions<AltinnOptions> altinnOptions,
        ILogger<AltinnTokenExchangeService> logger)
    {
        _httpClient = httpClient;
        _altinnOptions = altinnOptions.Value;
        _logger = logger;
    }

    public async Task<string?> ExchangeIdPortenToken(string idPortenAccessToken, CancellationToken cancellationToken = default)
    {
        var exchangeUrl = $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/exchange/id-porten";

        using var request = new HttpRequestMessage(HttpMethod.Get, exchangeUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idPortenAccessToken);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(_altinnOptions.PlatformSubscriptionKey))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", _altinnOptions.PlatformSubscriptionKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Altinn ID-Porten token exchange failed. Status={StatusCode} Url={Url} BodyLength={BodyLength}. " +
                "Common causes: access token missing an altinn:* scope (Platform returns 403), " +
                "missing pid/acr claims, person not in Altinn Register, or missing PlatformSubscriptionKey (APIM 401).",
                (int)response.StatusCode,
                exchangeUrl,
                body.Length);
            return null;
        }

        try
        {
            return ParseAltinnExchangeToken(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse Altinn token exchange response. BodyLength={BodyLength} LooksLikeJwt={LooksLikeJwt}",
                body.Length,
                body.TrimStart().StartsWith("eyJ", StringComparison.Ordinal));
            return null;
        }
    }

    private static string ParseAltinnExchangeToken(string body)
    {
        body = body.Trim();
        if (body.StartsWith("eyJ", StringComparison.Ordinal))
        {
            return body;
        }

        if (body.StartsWith('"'))
        {
            return JsonSerializer.Deserialize<string>(body)
                ?? throw new InvalidOperationException("Altinn token exchange returned an empty string token.");
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind == JsonValueKind.String)
        {
            return document.RootElement.GetString()
                ?? throw new InvalidOperationException("Altinn token exchange returned an empty string token.");
        }

        if (document.RootElement.TryGetProperty("access_token", out var accessToken))
        {
            return accessToken.GetString()
                ?? throw new InvalidOperationException("Altinn token exchange returned an empty access_token.");
        }

        throw new InvalidOperationException(
            $"Unexpected Altinn token exchange response shape: {document.RootElement.ValueKind}.");
    }
}
