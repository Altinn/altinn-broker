namespace Altinn.Broker.Core.Services;

public interface IDigdirMaskinportenAdminService
{
    Task<MaskinportenJwkSet> GetJwksAsync(string clientId, MaskinportenAdminApiCredentials adminCredentials, CancellationToken cancellationToken);

    Task<MaskinportenJwkSet> UpdateJwksAsync(string clientId, MaskinportenJwkSet jwks, MaskinportenAdminApiCredentials adminCredentials, CancellationToken cancellationToken);
}

public interface IKeyVaultSecretStore
{
    Task<string?> GetSecretValueAsync(string vaultUrl, string secretName, CancellationToken cancellationToken);

    Task SetSecretAsync(string vaultUrl, string secretName, string value, CancellationToken cancellationToken);
}

public interface IMaskinportenJwkGenerator
{
    MaskinportenGeneratedJwk Generate(string keyIdPrefix);

    MaskinportenJwkKey GetPublicKey(string encodedPrivateJwk);
}

public interface IMaskinportenJwkRotationService
{
    Task<MaskinportenJwkRotationResult> RotateAsync(CancellationToken cancellationToken);
}

public interface IMaskinportenTokenService
{
    Task<string> RequestTokenAsync(string clientId, string encodedPrivateJwk, string scope, string environment, CancellationToken cancellationToken);
}

public interface IContainerAppRefreshService
{
    Task RefreshAsync(string containerAppResourceId, string reason, CancellationToken cancellationToken);
}
