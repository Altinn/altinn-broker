using System.Security.Claims;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.ProvisionServiceOwner;

public class StartServiceOwnerProvisioningRequest
{
    public required string Party { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// Registers the selected organization as a Broker service owner and starts storage deployment.
/// The caller must have publish on digdir-broker-administrasjon for that organization. The
/// organization does not need to own a broker service yet. Repeating the call while deployment
/// is running does not start a second deployment.
/// </summary>
public class StartServiceOwnerProvisioningHandler(
    IServiceOwnerRepository serviceOwnerRepository,
    IAuthorizationService authorizationService,
    IResourceManager resourceManager,
    IHostEnvironment hostEnvironment,
    ILogger<StartServiceOwnerProvisioningHandler> logger) : IHandler<StartServiceOwnerProvisioningRequest, ServiceOwnerProvisioningState>
{
    public const int MaxNameLength = 500;

    public async Task<OneOf<ServiceOwnerProvisioningState, Error>> Process(
        StartServiceOwnerProvisioningRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var party = request.Party.WithoutPrefix();
        if (!party.IsOrganizationNumber())
        {
            return Errors.InvalidParty;
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            return ServiceOwnerErrors.ServiceOwnerNameMissing;
        }

        var gatekeeper = await HasGatekeeperPublish(user, party, cancellationToken);
        if (gatekeeper.IsT1)
        {
            return gatekeeper.AsT1;
        }
        if (!gatekeeper.AsT0)
        {
            return Errors.NoPublishAccessToConfigureResource;
        }

        var serviceOwnerId = party.WithPrefix();
        var inserted = await serviceOwnerRepository.TryInitializeServiceOwner(serviceOwnerId, name);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId);
        if (serviceOwner is null)
        {
            return ServiceOwnerErrors.ServiceOwnerInitializationFailed;
        }

        var phase = ServiceOwnerProvisioning.EffectivePhase(serviceOwner, hostEnvironment.IsDevelopment());
        if (phase == ProvisioningPhase.Ready)
        {
            if (serviceOwner.ProvisioningPhase != ProvisioningPhase.Ready)
            {
                await serviceOwnerRepository.SetProvisioningPhase(serviceOwner.Id, ProvisioningPhase.Ready);
            }

            return ServiceOwnerProvisioningState.FromPhase(ProvisioningPhase.Ready);
        }

        var inProgress = phase is not ProvisioningPhase.Failed;
        if (inProgress && !inserted)
        {
            return ServiceOwnerProvisioningState.FromPhase(phase);
        }

        if (phase == ProvisioningPhase.Failed)
        {
            await serviceOwnerRepository.SetProvisioningPhase(serviceOwner.Id, ProvisioningPhase.Queued);
        }

        serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId) ?? serviceOwner;
        try
        {
            resourceManager.CreateStorageProviders(serviceOwner, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not start storage deployment for service owner {ServiceOwnerId}", serviceOwnerId);
            await serviceOwnerRepository.SetProvisioningPhase(serviceOwnerId, ProvisioningPhase.Failed);
            return ServiceOwnerErrors.ServiceOwnerInitializationFailed;
        }

        logger.LogInformation("Started provisioning for service owner {ServiceOwnerId}", serviceOwnerId);
        return ServiceOwnerProvisioningState.FromPhase(ProvisioningPhase.Queued);
    }

    private async Task<OneOf<bool, Error>> HasGatekeeperPublish(ClaimsPrincipal? user, string party, CancellationToken cancellationToken)
    {
        try
        {
            return await authorizationService.CheckAccessAsPublisher(
                user,
                ApplicationConstants.BrokerBoxConfigureGatekeeperResourceId,
                party,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not evaluate publish access on configuration gatekeeper for party {Party}", party.SanitizeForLogs());
            return Errors.AuthorizationUnavailable;
        }
    }
}
