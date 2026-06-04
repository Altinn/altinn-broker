using Altinn.Broker.Core.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Altinn.Broker.Integrations.Maskinporten;

public class DigdirMaskinportenAdminService(
    IHttpClientFactory httpClientFactory,
    IMaskinportenTokenService tokenService,
    ILogger<DigdirMaskinportenAdminService> logger) : IDigdirMaskinportenAdminService
{
    public async Task<MaskinportenJwkSet> GetJwksAsync(string clientId, MaskinportenAdminApiCredentials adminCredentials, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"clients/{clientId}/jwks", adminCredentials, cancellationToken);
        using var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MaskinportenJwkSet>(cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Unable to deserialize Digdir JWKS for client '{clientId}'.");
    }

    public async Task<MaskinportenJwkSet> UpdateJwksAsync(string clientId, MaskinportenJwkSet jwks, MaskinportenAdminApiCredentials adminCredentials, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, $"clients/{clientId}/jwks", adminCredentials, cancellationToken);
        request.Content = JsonContent.Create(jwks);
        using var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MaskinportenJwkSet>(cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Unable to deserialize updated Digdir JWKS for client '{clientId}'.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string relativePath,
        MaskinportenAdminApiCredentials adminCredentials,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.RequestTokenAsync(
            adminCredentials.ClientId,
            adminCredentials.EncodedJwk,
            adminCredentials.Scope,
            adminCredentials.Environment,
            cancellationToken);

        var request = new HttpRequestMessage(method, $"{GetDigdirApiBaseUrl(adminCredentials.ApiBaseUrl, adminCredentials.Environment)}/{relativePath}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Digdir admin request failed. Method={Method}, Url={Url}, Status={StatusCode}, Body={Body}",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException($"Digdir admin request failed with status {(int)response.StatusCode}: {body}");
        }

        return response;
    }

    private static string GetDigdirApiBaseUrl(string configuredBaseUrl, string environment)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        return environment.Equals("prod", StringComparison.OrdinalIgnoreCase)
            || environment.Equals("production", StringComparison.OrdinalIgnoreCase)
            ? "https://api.samarbeid.digdir.no/external/api/v1"
            : "https://api.test.samarbeid.digdir.no/external/api/v1";
    }
}
