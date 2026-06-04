namespace Altinn.Broker.Core.Options;

public class MaskinportenJwkRotationSettings
{
    public bool Enabled { get; set; }

    public int VerificationMaxAttempts { get; set; } = 12;

    public int VerificationDelaySeconds { get; set; } = 15;

    public string AdminClientId { get; set; } = string.Empty;

    public string AdminEncodedJwk { get; set; } = string.Empty;

    public string AdminScope { get; set; } = "idporten:dcr.write";

    public string AdminApiBaseUrl { get; set; } = string.Empty;

    public string AdminKeyVaultSecretName { get; set; } = "maskinporten-admin-jwk";

    public string AdminClientIdKeyVaultSecretName { get; set; } = "maskinporten-admin-client-id";

    public string AdminNewKeyIdPrefix { get; set; } = "altinn-broker-maskinporten-admin";

    public string KeyVaultUrl { get; set; } = string.Empty;

    public string ContainerAppResourceId { get; set; } = string.Empty;

    public bool RefreshContainerAppsAfterRotation { get; set; } = true;

    public string KeyVaultSecretName { get; set; } = "maskinporten-jwk";

    public string TargetClientIdKeyVaultSecretName { get; set; } = "maskinporten-client-id";

    public string NewKeyIdPrefix { get; set; } = "altinn-broker-maskinporten";

    public List<MaskinportenJwkRotationTarget> Targets { get; set; } = [];
}

public class MaskinportenJwkRotationTarget
{
    public string Name { get; set; } = string.Empty;

    public string KeyVaultUrl { get; set; } = string.Empty;

    public string ClientIdSecretName { get; set; } = string.Empty;

    public string EncodedJwkSecretName { get; set; } = string.Empty;

    public string ContainerAppResourceId { get; set; } = string.Empty;
}
