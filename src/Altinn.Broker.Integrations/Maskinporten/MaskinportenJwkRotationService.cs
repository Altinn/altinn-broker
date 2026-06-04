using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.ExceptionServices;

namespace Altinn.Broker.Integrations.Maskinporten;

public class MaskinportenJwkRotationService(
    IOptions<MaskinportenJwkRotationSettings> rotationOptions,
    IOptions<MaskinportenSettings> targetOptions,
    IDigdirMaskinportenAdminService digdirMaskinportenAdminService,
    IMaskinportenJwkGenerator jwkGenerator,
    IMaskinportenTokenService tokenService,
    IKeyVaultSecretStore keyVaultSecretStore,
    IContainerAppRefreshService containerAppRefreshService,
    ILogger<MaskinportenJwkRotationService> logger) : IMaskinportenJwkRotationService
{
    public async Task<MaskinportenJwkRotationResult> RotateAsync(CancellationToken cancellationToken)
    {
        var settings = rotationOptions.Value;
        var target = targetOptions.Value;
        ValidateConfiguration(settings, target);
        var leaderKeyVaultUrl = NormalizeKeyVaultUrl(settings.KeyVaultUrl);
        await PreflightKeyVaultSecretsAsync(
            leaderKeyVaultUrl,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [settings.AdminKeyVaultSecretName] = null,
                [settings.AdminClientIdKeyVaultSecretName] = settings.AdminClientId
            },
            cancellationToken);
        var targetRotation = await GetRotationTargetAsync(settings, target, cancellationToken);

        var adminCredentials = CreateAdminCredentials(settings, target.Environment, settings.AdminEncodedJwk);
        var adminRotation = await RotateClientAsync(
            new RotationTarget(
                settings.AdminClientId,
                "Maskinporten admin",
                settings.AdminEncodedJwk,
                settings.AdminScope,
                [
                    new KeyVaultWriteTarget(
                        "Maskinporten admin",
                        leaderKeyVaultUrl,
                        settings.AdminKeyVaultSecretName,
                        settings.ContainerAppResourceId)
                ],
                settings.AdminNewKeyIdPrefix,
                target.Environment),
            adminCredentials,
            generated => VerifyAdminDcrAccessAsync(settings, target.Environment, generated, cancellationToken),
            cancellationToken);

        adminCredentials = CreateAdminCredentials(settings, target.Environment, adminRotation.Generated.PrivateJwkBase64);
        var clientResults = new List<MaskinportenJwkRotationClientResult> { adminRotation.Result };
        try
        {
            var rotation = await RotateClientAsync(
                targetRotation,
                adminCredentials,
                generated => tokenService.RequestTokenAsync(
                    targetRotation.ClientId,
                    generated.PrivateJwkBase64,
                    targetRotation.VerificationScope,
                    targetRotation.Environment,
                    cancellationToken),
                cancellationToken);
            clientResults.Add(rotation.Result);

            await RefreshCompletedRotationsAsync(settings, clientResults, cancellationToken);
        }
        catch
        {
            await RefreshCompletedRotationsAsync(settings, clientResults, cancellationToken);
            throw;
        }

        return new MaskinportenJwkRotationResult
        {
            Clients = clientResults
        };
    }

    private async Task RefreshCompletedRotationsAsync(
        MaskinportenJwkRotationSettings settings,
        IReadOnlyList<MaskinportenJwkRotationClientResult> clientResults,
        CancellationToken cancellationToken)
    {
        if (!settings.RefreshContainerAppsAfterRotation)
        {
            return;
        }

        foreach (var refreshTarget in clientResults
            .SelectMany(result =>
                (result.ContainerAppResourceIds.Count > 0 ? result.ContainerAppResourceIds : [result.ContainerAppResourceId])
                    .Select(containerAppResourceId => new
                    {
                        ContainerAppResourceId = containerAppResourceId,
                        Reason = $"Maskinporten JWK rotation for {result.ClientName}"
                    }))
            .Where(target => !string.IsNullOrWhiteSpace(target.ContainerAppResourceId))
            .DistinctBy(target => target.ContainerAppResourceId, StringComparer.OrdinalIgnoreCase))
        {
            await containerAppRefreshService.RefreshAsync(
                refreshTarget.ContainerAppResourceId,
                refreshTarget.Reason,
                cancellationToken);
        }
    }

    private static IEnumerable<MaskinportenJwkKey> MergeDistinctKeys(
        IEnumerable<MaskinportenJwkKey> existingKeys,
        params MaskinportenJwkKey[] keys)
        => existingKeys
            .Concat(keys)
            .GroupBy(key => key.Kid, StringComparer.Ordinal)
            .Select(group => group.Last());

    private async Task<SingleRotationExecution> RotateClientAsync(
        RotationTarget rotationTarget,
        MaskinportenAdminApiCredentials adminCredentials,
        Func<MaskinportenGeneratedJwk, Task> verifyNewKey,
        CancellationToken cancellationToken)
    {
        var settings = rotationOptions.Value;
        var currentPublicKey = jwkGenerator.GetPublicKey(rotationTarget.CurrentEncodedJwk);
        var originalSecretValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var updatedSecretTargets = new List<KeyVaultWriteTarget>();

        logger.LogInformation("Starting Maskinporten JWK rotation for client {ClientId}.", rotationTarget.ClientId);
        var originalJwks = await digdirMaskinportenAdminService.GetJwksAsync(rotationTarget.ClientId, adminCredentials, cancellationToken);
        var generated = jwkGenerator.Generate(rotationTarget.NewKeyIdPrefix);

        var rotatedJwks = new MaskinportenJwkSet
        {
            Keys = [.. MergeDistinctKeys(originalJwks.Keys, currentPublicKey, generated.PublicJwk)]
        };

        var jwksUpdated = false;
        try
        {
            var updatedJwks = await digdirMaskinportenAdminService.UpdateJwksAsync(rotationTarget.ClientId, rotatedJwks, adminCredentials, cancellationToken);
            jwksUpdated = true;

            logger.LogInformation(
                "Maskinporten JWKS updated for client {ClientId}. Returned kids: {Kids}.",
                rotationTarget.ClientId,
                FormatKids(updatedJwks.Keys));

            var verifiedJwks = await VerifyNewKeyAsync(
                settings,
                rotationTarget.ClientId,
                generated,
                adminCredentials,
                verifyNewKey,
                cancellationToken);

            foreach (var keyVaultTarget in rotationTarget.KeyVaultTargets)
            {
                originalSecretValues[GetSecretTargetKey(keyVaultTarget)] = await keyVaultSecretStore.GetSecretValueAsync(
                    keyVaultTarget.KeyVaultUrl,
                    keyVaultTarget.KeyVaultSecretName,
                    cancellationToken);

                await keyVaultSecretStore.SetSecretAsync(
                    keyVaultTarget.KeyVaultUrl,
                    keyVaultTarget.KeyVaultSecretName,
                    generated.PrivateJwkBase64,
                    cancellationToken);
                updatedSecretTargets.Add(keyVaultTarget);
            }

            logger.LogInformation(
                "Completed Maskinporten JWK rotation for client {ClientId}. New kid={Kid}. Keys before={Before}, keys after={After}. Key Vaults updated: {KeyVaultUrls}.",
                rotationTarget.ClientId,
                generated.Kid,
                originalJwks.Keys.Count,
                verifiedJwks.Keys.Count,
                string.Join(", ", rotationTarget.KeyVaultTargets.Select(target => target.KeyVaultUrl)));

            var primaryKeyVaultTarget = rotationTarget.KeyVaultTargets[0];
            var containerAppResourceIds = rotationTarget.KeyVaultTargets
                .Select(target => target.ContainerAppResourceId)
                .Where(resourceId => !string.IsNullOrWhiteSpace(resourceId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new SingleRotationExecution(
                new MaskinportenJwkRotationClientResult
                {
                    ClientId = rotationTarget.ClientId,
                    ClientName = rotationTarget.ClientName,
                    NewKid = generated.Kid,
                    PreviousKeyCount = originalJwks.Keys.Count,
                    CurrentKeyCount = verifiedJwks.Keys.Count,
                    VerificationScope = rotationTarget.VerificationScope,
                    KeyVaultUrl = primaryKeyVaultTarget.KeyVaultUrl,
                    KeyVaultSecretName = primaryKeyVaultTarget.KeyVaultSecretName,
                    ContainerAppResourceId = primaryKeyVaultTarget.ContainerAppResourceId,
                    ContainerAppResourceIds = containerAppResourceIds
                },
                generated);
        }
        catch (Exception ex)
        {
            List<Exception>? rollbackFailures = null;
            var keyVaultRollbackFailed = false;

            foreach (var keyVaultTarget in updatedSecretTargets.AsEnumerable().Reverse())
            {
                logger.LogWarning(
                    "Maskinporten JWK rotation failed after updating Key Vault secret. Restoring previous secret value for vault: {KeyVaultUrl}.",
                    keyVaultTarget.KeyVaultUrl);

                var originalSecretValue = originalSecretValues[GetSecretTargetKey(keyVaultTarget)];
                if (string.IsNullOrWhiteSpace(originalSecretValue))
                {
                    var rollbackException = new InvalidOperationException(
                        $"Cannot restore Maskinporten JWK secret for vault {keyVaultTarget.KeyVaultUrl} because the original secret value was missing.");
                    keyVaultRollbackFailed = true;
                    rollbackFailures ??= [];
                    rollbackFailures.Add(rollbackException);
                    logger.LogError(rollbackException, "Failed to restore Maskinporten JWK secret for vault {KeyVaultUrl}.", keyVaultTarget.KeyVaultUrl);
                }
                else
                {
                    try
                    {
                        await keyVaultSecretStore.SetSecretAsync(
                            keyVaultTarget.KeyVaultUrl,
                            keyVaultTarget.KeyVaultSecretName,
                            originalSecretValue,
                            cancellationToken);
                    }
                    catch (Exception rollbackEx)
                    {
                        keyVaultRollbackFailed = true;
                        rollbackFailures ??= [];
                        rollbackFailures.Add(rollbackEx);
                        logger.LogError(rollbackEx, "Failed to restore Maskinporten JWK secret for vault {KeyVaultUrl}.", keyVaultTarget.KeyVaultUrl);
                    }
                }
            }

            if (jwksUpdated && keyVaultRollbackFailed)
            {
                logger.LogWarning(
                    "Keeping updated JWKS for client {ClientId} because one or more Key Vault secret rollback operations failed.",
                    rotationTarget.ClientId);
            }
            else if (jwksUpdated)
            {
                logger.LogWarning("Maskinporten JWK rotation failed after JWKS update. Restoring original JWKS for client {ClientId}.", rotationTarget.ClientId);
                try
                {
                    await digdirMaskinportenAdminService.UpdateJwksAsync(rotationTarget.ClientId, originalJwks, adminCredentials, cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    rollbackFailures ??= [];
                    rollbackFailures.Add(rollbackEx);
                    logger.LogError(rollbackEx, "Failed to restore original JWKS for client {ClientId}.", rotationTarget.ClientId);
                }
            }

            if (rollbackFailures is not null)
            {
                rollbackFailures.Insert(0, ex);
                throw new AggregateException("Maskinporten JWK rotation failed and one or more rollback operations also failed.", rollbackFailures);
            }

            throw;
        }
    }

    private async Task<MaskinportenJwkSet> VerifyNewKeyAsync(
        MaskinportenJwkRotationSettings settings,
        string targetClientId,
        MaskinportenGeneratedJwk generated,
        MaskinportenAdminApiCredentials adminCredentials,
        Func<MaskinportenGeneratedJwk, Task> verifyNewKey,
        CancellationToken cancellationToken)
    {
        Exception? lastRetryableException = null;

        for (var attempt = 1; attempt <= settings.VerificationMaxAttempts; attempt++)
        {
            try
            {
                var currentJwks = await digdirMaskinportenAdminService.GetJwksAsync(targetClientId, adminCredentials, cancellationToken);
                var kidPresent = currentJwks.Keys.Any(key => string.Equals(key.Kid, generated.Kid, StringComparison.Ordinal));

                logger.LogInformation(
                    "Verifying Maskinporten JWK rotation for client {ClientId}. Attempt {Attempt}/{MaxAttempts}. New kid present in JWKS: {KidPresent}. Current kids: {Kids}.",
                    targetClientId,
                    attempt,
                    settings.VerificationMaxAttempts,
                    kidPresent,
                    FormatKids(currentJwks.Keys));

                await verifyNewKey(generated);

                logger.LogInformation(
                    "Verified Maskinporten JWK rotation for client {ClientId} with kid {Kid} on attempt {Attempt}/{MaxAttempts}.",
                    targetClientId,
                    generated.Kid,
                    attempt,
                    settings.VerificationMaxAttempts);

                return currentJwks;
            }
            catch (Exception ex) when (IsRetryableVerificationFailure(ex) && attempt < settings.VerificationMaxAttempts)
            {
                lastRetryableException = ex;

                logger.LogWarning(
                    ex,
                    "Verification attempt {Attempt}/{MaxAttempts} failed for client {ClientId} and will be retried in {DelaySeconds}s.",
                    attempt,
                    settings.VerificationMaxAttempts,
                    targetClientId,
                    settings.VerificationDelaySeconds);

                if (settings.VerificationDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(settings.VerificationDelaySeconds), cancellationToken);
                }
            }
        }

        if (lastRetryableException is not null)
        {
            ExceptionDispatchInfo.Capture(lastRetryableException).Throw();
        }

        throw new InvalidOperationException("Maskinporten JWK rotation verification failed without a retryable exception.");
    }

    private async Task VerifyAdminDcrAccessAsync(
        MaskinportenJwkRotationSettings settings,
        string environment,
        MaskinportenGeneratedJwk generated,
        CancellationToken cancellationToken)
    {
        var updatedAdminCredentials = CreateAdminCredentials(settings, environment, generated.PrivateJwkBase64);
        await digdirMaskinportenAdminService.GetJwksAsync(settings.AdminClientId, updatedAdminCredentials, cancellationToken);
    }

    private async Task<RotationTarget> GetRotationTargetAsync(
        MaskinportenJwkRotationSettings settings,
        MaskinportenSettings primaryTarget,
        CancellationToken cancellationToken)
    {
        var verificationScope = GetVerificationScope(primaryTarget);
        var keyVaultTargets = new List<KeyVaultWriteTarget>
        {
            new(
                "Broker",
                NormalizeKeyVaultUrl(settings.KeyVaultUrl),
                settings.KeyVaultSecretName,
                settings.ContainerAppResourceId)
        };

        await PreflightKeyVaultSecretsAsync(
            keyVaultTargets[0].KeyVaultUrl,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [settings.KeyVaultSecretName] = null,
                [settings.TargetClientIdKeyVaultSecretName] = primaryTarget.ClientId
            },
            cancellationToken);

        foreach (var configuredTarget in settings.Targets)
        {
            var keyVaultUrl = NormalizeKeyVaultUrl(configuredTarget.KeyVaultUrl);
            var clientIdSecretName = string.IsNullOrWhiteSpace(configuredTarget.ClientIdSecretName)
                ? settings.TargetClientIdKeyVaultSecretName
                : configuredTarget.ClientIdSecretName;
            var encodedJwkSecretName = string.IsNullOrWhiteSpace(configuredTarget.EncodedJwkSecretName)
                ? settings.KeyVaultSecretName
                : configuredTarget.EncodedJwkSecretName;

            await PreflightKeyVaultSecretsAsync(
                keyVaultUrl,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [clientIdSecretName] = primaryTarget.ClientId,
                    [encodedJwkSecretName] = null
                },
                cancellationToken);

            keyVaultTargets.Add(new KeyVaultWriteTarget(
                string.IsNullOrWhiteSpace(configuredTarget.Name) ? keyVaultUrl : configuredTarget.Name,
                keyVaultUrl,
                encodedJwkSecretName,
                configuredTarget.ContainerAppResourceId));
        }

        return new RotationTarget(
            primaryTarget.ClientId,
            "Broker",
            primaryTarget.EncodedJwk,
            verificationScope,
            keyVaultTargets,
            settings.NewKeyIdPrefix,
            primaryTarget.Environment);
    }

    private async Task PreflightKeyVaultSecretsAsync(
        string keyVaultUrl,
        IReadOnlyDictionary<string, string?> secretRequirements,
        CancellationToken cancellationToken)
    {
        foreach (var secretRequirement in secretRequirements)
        {
            var secretName = secretRequirement.Key;
            var secretValue = await keyVaultSecretStore.GetSecretValueAsync(keyVaultUrl, secretName, cancellationToken);

            if (secretRequirement.Value is not null
                && !string.Equals(secretValue, secretRequirement.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Key Vault {keyVaultUrl} contains unexpected value for secret {secretName} during Maskinporten rotation preflight.");
            }
        }
    }

    private static MaskinportenAdminApiCredentials CreateAdminCredentials(
        MaskinportenJwkRotationSettings settings,
        string environment,
        string encodedJwk)
        => new()
        {
            ClientId = settings.AdminClientId,
            EncodedJwk = encodedJwk,
            Scope = settings.AdminScope,
            ApiBaseUrl = settings.AdminApiBaseUrl,
            Environment = environment
        };

    private static string GetVerificationScope(MaskinportenSettings target)
    {
        var firstScope = target.Scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return firstScope
            ?? throw new InvalidOperationException("No verification scope configured for Maskinporten JWK rotation.");
    }

    private static string NormalizeKeyVaultUrl(string url)
        => string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : url.Trim().TrimEnd('/');

    private static bool IsRetryableVerificationFailure(Exception exception)
        => exception is InvalidOperationException invalidOperationException
            && invalidOperationException.Message.Contains("Unknown key identifier (kid)", StringComparison.OrdinalIgnoreCase);

    private static string FormatKids(IEnumerable<MaskinportenJwkKey> keys)
    {
        var kids = keys
            .Select(key => string.IsNullOrWhiteSpace(key.Kid) ? "<missing>" : key.Kid)
            .ToArray();

        return kids.Length == 0 ? "<none>" : string.Join(", ", kids);
    }

    private static string GetSecretTargetKey(KeyVaultWriteTarget target)
        => $"{target.KeyVaultUrl}|{target.KeyVaultSecretName}";

    private static void ValidateConfiguration(MaskinportenJwkRotationSettings settings, MaskinportenSettings target)
    {
        if (string.IsNullOrWhiteSpace(settings.AdminClientId))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin client id is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminEncodedJwk))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin JWK is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminScope))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin scope is missing.");
        }

        if (string.IsNullOrWhiteSpace(NormalizeKeyVaultUrl(settings.KeyVaultUrl)))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation Key Vault URL is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminKeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminClientIdKeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin client id Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.KeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.TargetClientIdKeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation target client id Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminNewKeyIdPrefix))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin key id prefix is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.NewKeyIdPrefix))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation key id prefix is missing.");
        }

        foreach (var configuredTarget in settings.Targets)
        {
            if (string.IsNullOrWhiteSpace(configuredTarget.KeyVaultUrl))
            {
                throw new InvalidOperationException("Maskinporten JWK rotation target Key Vault URL is missing.");
            }
        }

        if (settings.VerificationMaxAttempts <= 0)
        {
            throw new InvalidOperationException("Maskinporten JWK rotation verification max attempts must be greater than zero.");
        }

        if (settings.VerificationDelaySeconds < 0)
        {
            throw new InvalidOperationException("Maskinporten JWK rotation verification delay seconds cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(target.ClientId))
        {
            throw new InvalidOperationException("Target Maskinporten client id is missing.");
        }

        if (string.IsNullOrWhiteSpace(target.EncodedJwk))
        {
            throw new InvalidOperationException("Target Maskinporten private JWK is missing.");
        }

        if (string.IsNullOrWhiteSpace(target.Scope))
        {
            throw new InvalidOperationException("Target Maskinporten scope is missing.");
        }

        if (string.IsNullOrWhiteSpace(target.Environment))
        {
            throw new InvalidOperationException("Target Maskinporten environment is missing.");
        }
    }

    private sealed record RotationTarget(
        string ClientId,
        string ClientName,
        string CurrentEncodedJwk,
        string VerificationScope,
        IReadOnlyList<KeyVaultWriteTarget> KeyVaultTargets,
        string NewKeyIdPrefix,
        string Environment);

    private sealed record KeyVaultWriteTarget(
        string Name,
        string KeyVaultUrl,
        string KeyVaultSecretName,
        string ContainerAppResourceId);

    private sealed record SingleRotationExecution(
        MaskinportenJwkRotationClientResult Result,
        MaskinportenGeneratedJwk Generated);
}
