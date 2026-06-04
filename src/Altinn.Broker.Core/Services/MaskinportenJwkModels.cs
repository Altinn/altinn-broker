using System.Text.Json.Serialization;

namespace Altinn.Broker.Core.Services;

public class MaskinportenJwkKey
{
    [JsonPropertyName("kty")]
    public string Kty { get; set; } = string.Empty;

    [JsonPropertyName("kid")]
    public string Kid { get; set; } = string.Empty;

    [JsonPropertyName("use")]
    public string Use { get; set; } = string.Empty;

    [JsonPropertyName("alg")]
    public string Alg { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string N { get; set; } = string.Empty;

    [JsonPropertyName("e")]
    public string E { get; set; } = string.Empty;
}

public class MaskinportenJwkSet
{
    [JsonPropertyName("keys")]
    public List<MaskinportenJwkKey> Keys { get; set; } = [];
}

public class MaskinportenGeneratedJwk
{
    public string Kid { get; init; } = string.Empty;

    public string PrivateJwkJson { get; init; } = string.Empty;

    public string PrivateJwkBase64 { get; init; } = string.Empty;

    public MaskinportenJwkKey PublicJwk { get; init; } = new();
}

public class MaskinportenAdminApiCredentials
{
    public string ClientId { get; init; } = string.Empty;

    public string EncodedJwk { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;
}

public class MaskinportenJwkRotationClientResult
{
    public string ClientId { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public string NewKid { get; init; } = string.Empty;

    public int PreviousKeyCount { get; init; }

    public int CurrentKeyCount { get; init; }

    public string VerificationScope { get; init; } = string.Empty;

    public string KeyVaultUrl { get; init; } = string.Empty;

    public string KeyVaultSecretName { get; init; } = string.Empty;

    public string ContainerAppResourceId { get; init; } = string.Empty;

    public IReadOnlyList<string> ContainerAppResourceIds { get; init; } = [];
}

public class MaskinportenJwkRotationResult
{
    public IReadOnlyList<MaskinportenJwkRotationClientResult> Clients { get; init; } = [];
}
