using Altinn.Broker.Core.Services;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Altinn.Broker.Integrations.Azure;

public class KeyVaultSecretStore : IKeyVaultSecretStore
{
    public async Task<string?> GetSecretValueAsync(string vaultUrl, string secretName, CancellationToken cancellationToken)
    {
        var client = CreateSecretClient(vaultUrl, secretName);
        var response = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return response.Value.Value;
    }

    public async Task SetSecretAsync(string vaultUrl, string secretName, string value, CancellationToken cancellationToken)
    {
        var client = CreateSecretClient(vaultUrl, secretName);
        await client.SetSecretAsync(secretName, value, cancellationToken);
    }

    private static SecretClient CreateSecretClient(string vaultUrl, string secretName)
    {
        if (string.IsNullOrWhiteSpace(vaultUrl))
        {
            throw new InvalidOperationException("Key Vault URL is missing for Maskinporten JWK rotation.");
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new InvalidOperationException("Key Vault secret name is missing for Maskinporten JWK rotation.");
        }

        return new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
    }
}
