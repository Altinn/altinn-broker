using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.Tests.LargeFile;

public sealed record AltinnAuthOptions(
    string BaseUrl,
    string ClientId,
    string ClientKid,
    string ClientPrivateKeyPem,
    string OrgNumber,
    string? MaskinportenTokenUrl);

/// <summary>
/// Obtains Altinn API tokens via Maskinporten JWT bearer grant and Altinn token exchange,
/// matching the Bruno authentication flow in <c>.bruno/Authentication</c>.
/// </summary>
public static class AltinnAuthClient
{
    private const string BrokerWriteScope = "altinn:broker.write";

    public static AltinnAuthOptions ReadOptionsFromEnvironment()
    {
        return new AltinnAuthOptions(
            BaseUrl: ReadEnv("BASE_URL", "https://altinn-dev-api.azure-api.net"),
            ClientId: RequireEnv("CLIENT_ID"),
            ClientKid: RequireEnv("CLIENT_KID"),
            ClientPrivateKeyPem: ReadPrivateKeyPem(),
            OrgNumber: RequireEnv("ORG_NO"),
            MaskinportenTokenUrl: Environment.GetEnvironmentVariable("MASKINPORTEN_TOKEN_URL"));
    }

    public static async Task<string> ExchangeAltinnTokenAsync(
        HttpClient httpClient,
        AltinnAuthOptions options,
        CancellationToken cancellationToken = default)
    {
        var maskinportenToken = await RequestMaskinportenTokenAsync(httpClient, options, cancellationToken);
        return await ExchangeMaskinportenTokenAsync(
            httpClient,
            options.BaseUrl,
            maskinportenToken,
            cancellationToken);
    }

    private static async Task<string> RequestMaskinportenTokenAsync(
        HttpClient httpClient,
        AltinnAuthOptions options,
        CancellationToken cancellationToken)
    {
        var tokenUrl = ResolveMaskinportenTokenUrl(options);
        var assertion = CreateMaskinportenClientAssertion(options);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion,
                }),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable || attempt == 1)
            {
                var payload = await response.Content.ReadFromJsonAsync<MaskinportenTokenResponse>(
                    cancellationToken: cancellationToken);

                if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(payload?.AccessToken))
                {
                    throw new InvalidOperationException(
                        $"Maskinporten token request failed. Status={(int)response.StatusCode}. " +
                        $"Error={payload?.Error}. Description={payload?.ErrorDescription}");
                }

                return payload.AccessToken;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new InvalidOperationException("Maskinporten token request failed after retry.");
    }

    private static async Task<string> ExchangeMaskinportenTokenAsync(
        HttpClient httpClient,
        string baseUrl,
        string maskinportenToken,
        CancellationToken cancellationToken)
    {
        var exchangeUrl = $"{baseUrl.TrimEnd('/')}/authentication/api/v1/exchange/maskinporten";
        using var request = new HttpRequestMessage(HttpMethod.Get, exchangeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", maskinportenToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException(
                $"Altinn token exchange failed. Status={(int)response.StatusCode}. Body={body}");
        }

        return ParseAltinnExchangeToken(body);
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

        throw new InvalidOperationException($"Unexpected Altinn token exchange response: {body}");
    }

    private static string CreateMaskinportenClientAssertion(AltinnAuthOptions options)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(options.ClientPrivateKeyPem);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["aud"] = GetMaskinportenAudience(options),
                ["scope"] = BrokerWriteScope,
                ["iss"] = options.ClientId,
                ["iat"] = now,
                ["exp"] = now + 120,
                ["authorization_details"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "urn:altinn:systemuser",
                        ["systemuser_org"] = new Dictionary<string, object>
                        {
                            ["authority"] = "iso6523-actorid-upis",
                            ["ID"] = options.OrgNumber,
                        },
                    },
                },
            },
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa) { KeyId = options.ClientKid },
                SecurityAlgorithms.RsaSha256),
        });
    }

    private static string ResolveMaskinportenTokenUrl(AltinnAuthOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.MaskinportenTokenUrl))
        {
            return options.MaskinportenTokenUrl;
        }

        return IsProductionPlatform(options.BaseUrl)
            ? "https://maskinporten.no/token"
            : "https://test.maskinporten.no/token";
    }

    private static string GetMaskinportenAudience(AltinnAuthOptions options)
    {
        var tokenUrl = ResolveMaskinportenTokenUrl(options);
        var uri = new Uri(tokenUrl);
        return $"{uri.Scheme}://{uri.Host}/";
    }

    private static bool IsProductionPlatform(string baseUrl)
        => baseUrl.Contains("platform.altinn.no", StringComparison.OrdinalIgnoreCase)
            && !baseUrl.Contains("tt02", StringComparison.OrdinalIgnoreCase);

    private static string ReadPrivateKeyPem()
    {
        var filePath = Environment.GetEnvironmentVariable("CLIENT_SECRET_FILE")
            ?? Environment.GetEnvironmentVariable("CLIENT_PEM_FILE");
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return File.ReadAllText(filePath);
        }

        var pem = Environment.GetEnvironmentVariable("CLIENT_SECRET")
            ?? Environment.GetEnvironmentVariable("CLIENT_PEM");
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new InvalidOperationException(
                "Missing required environment variable: CLIENT_SECRET (Maskinporten private key PEM). " +
                "CLIENT_PEM is also accepted. Use CLIENT_SECRET_FILE for a PEM file path.");
        }

        return pem.Replace("\\n", "\n", StringComparison.Ordinal);
    }

    private static string ReadEnv(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Missing required environment variable: {name}");
        }

        return value;
    }

    private sealed class MaskinportenTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}
