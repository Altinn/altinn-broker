using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application;

public enum BrokerResourceProvisionOutcome
{
    Exists,
    Created,
    NotFoundInRegistry,
    NotBrokerService,
    ServiceOwnerNotConfigured
}

/// <summary>
/// Ensures a BrokerService from Resource Registry exists locally with default Broker config.
/// </summary>
public class BrokerResourceProvisioner(
    IResourceRepository resourceRepository,
    IAltinnResourceRepository altinnResourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    ILogger<BrokerResourceProvisioner> logger)
{
    public async Task<(ResourceEntity? Resource, BrokerResourceProvisionOutcome Outcome)> EnsureAsync(
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var existing = await resourceRepository.GetResource(resourceId, cancellationToken);
        if (existing is not null)
        {
            return (existing, BrokerResourceProvisionOutcome.Exists);
        }

        var altinnResource = await altinnResourceRepository.GetResource(resourceId, cancellationToken);
        if (altinnResource is null)
        {
            return (null, BrokerResourceProvisionOutcome.NotFoundInRegistry);
        }
        if (!IsBrokerService(altinnResource.ResourceType))
        {
            return (null, BrokerResourceProvisionOutcome.NotBrokerService);
        }
        if (await serviceOwnerRepository.GetServiceOwner(altinnResource.ServiceOwnerId) is null)
        {
            return (null, BrokerResourceProvisionOutcome.ServiceOwnerNotConfigured);
        }

        return await CreateIgnoringConflictsAsync(altinnResource, cancellationToken);
    }

    /// <summary>
    /// Creates local default rows for BrokerService search hits that are missing and whose
    /// service owner is already configured in Broker. Failures for individual hits are skipped.
    /// </summary>
    public async Task EnsureFromSearchHitsAsync(
        IEnumerable<AltinnResourceSearchHit> hits,
        CancellationToken cancellationToken = default)
    {
        var existingResourceIds = (await resourceRepository.GetResources(cancellationToken))
            .Select(resource => resource.Id)
            .ToHashSet(StringComparer.Ordinal);
        var serviceOwnerConfiguredByOrg = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var hit in hits)
        {
            if (!IsBrokerService(hit.ResourceType) || existingResourceIds.Contains(hit.Id))
            {
                continue;
            }

            var serviceOwnerId = hit.OrganizationNumber.WithPrefix();
            if (!serviceOwnerConfiguredByOrg.TryGetValue(serviceOwnerId, out var serviceOwnerConfigured))
            {
                serviceOwnerConfigured = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId) is not null;
                serviceOwnerConfiguredByOrg[serviceOwnerId] = serviceOwnerConfigured;
            }

            if (!serviceOwnerConfigured)
            {
                logger.LogDebug(
                    "Skipping auto-create of BrokerService {resourceId}: service owner {serviceOwnerId} is not configured",
                    hit.Id.SanitizeForLogs(),
                    serviceOwnerId.SanitizeForLogs());
                continue;
            }

            var entity = new ResourceEntity
            {
                Id = hit.Id,
                ServiceOwnerId = serviceOwnerId,
                OrganizationNumber = hit.OrganizationNumber,
                ResourceType = hit.ResourceType
            };
            try
            {
                await CreateIgnoringConflictsAsync(entity, cancellationToken);
                existingResourceIds.Add(hit.Id);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // CreateIgnoringConflictsAsync already logged; skip this hit so listing continues.
            }
        }
    }

    private async Task<(ResourceEntity? Resource, BrokerResourceProvisionOutcome Outcome)> CreateIgnoringConflictsAsync(
        ResourceEntity entity,
        CancellationToken cancellationToken)
    {
        entity.MaxFileTransferSize ??= ApplicationConstants.MaxVirusScanUploadSize;

        try
        {
            var created = await resourceRepository.CreateResource(entity, cancellationToken);
            logger.LogInformation("Auto-created Broker resource {resourceId} with default configuration", entity.Id.SanitizeForLogs());
            return (created, BrokerResourceProvisionOutcome.Created);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var raced = await resourceRepository.GetResource(entity.Id, cancellationToken);
            if (raced is not null)
            {
                return (raced, BrokerResourceProvisionOutcome.Exists);
            }

            logger.LogWarning(
                ex,
                "Failed to auto-create Broker resource {resourceId}",
                entity.Id.SanitizeForLogs());
            throw;
        }
    }

    private static bool IsBrokerService(string? resourceType)
        => string.Equals(resourceType, AltinnResourceTypes.BrokerService, StringComparison.Ordinal);
}
