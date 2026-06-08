using Altinn.Broker.Core.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Maskinporten;

public class MaskinportenTokenService(IHttpClientFactory httpClientFactory) : IMaskinportenTokenService
{
    public async Task<string> RequestTokenAsync(string clientId, string encodedPrivateJwk, string scope, string environment, CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, GetTokenEndpoint(environment))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = CreateClientAssertion(clientId, encodedPrivateJwk, scope, environment)
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<MaskinportenTokenResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(payload?.AccessToken))
        {
            throw new InvalidOperationException(
                $"Maskinporten token request failed for client '{clientId}'. Status={(int)response.StatusCode}. Error={payload?.Error}. Description={payload?.ErrorDescription}");
        }

        return payload.AccessToken;
    }

    private static string CreateClientAssertion(string clientId, string encodedPrivateJwk, string scope, string environment)
    {
        var jwk = DecodePrivateJwk(encodedPrivateJwk);
        var rsaParameters = new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk["n"]),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk["e"]),
            D = Base64UrlEncoder.DecodeBytes(jwk["d"]),
            P = Base64UrlEncoder.DecodeBytes(jwk["p"]),
            Q = Base64UrlEncoder.DecodeBytes(jwk["q"]),
            DP = Base64UrlEncoder.DecodeBytes(jwk["dp"]),
            DQ = Base64UrlEncoder.DecodeBytes(jwk["dq"]),
            InverseQ = Base64UrlEncoder.DecodeBytes(jwk["qi"])
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["aud"] = GetTokenAudience(environment),
                ["scope"] = scope,
                ["iss"] = clientId,
                ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["exp"] = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(),
                ["jti"] = Guid.NewGuid().ToString()
            },
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsaParameters) { KeyId = jwk["kid"] },
                SecurityAlgorithms.RsaSha256)
        });
    }

    private static Dictionary<string, string> DecodePrivateJwk(string encodedPrivateJwk)
    {
        if (string.IsNullOrWhiteSpace(encodedPrivateJwk))
        {
            throw new InvalidOperationException("Encoded private JWK is missing.");
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPrivateJwk));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Unable to deserialize private JWK.");
    }

    private static string GetTokenEndpoint(string environment)
        => environment.Equals("prod", StringComparison.OrdinalIgnoreCase)
            || environment.Equals("production", StringComparison.OrdinalIgnoreCase)
            ? "https://maskinporten.no/token"
            : "https://test.maskinporten.no/token";

    private static string GetTokenAudience(string environment)
        => environment.Equals("prod", StringComparison.OrdinalIgnoreCase)
            || environment.Equals("production", StringComparison.OrdinalIgnoreCase)
            ? "https://maskinporten.no/"
            : "https://test.maskinporten.no/";

    private sealed class MaskinportenTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = string.Empty;
    }
}
