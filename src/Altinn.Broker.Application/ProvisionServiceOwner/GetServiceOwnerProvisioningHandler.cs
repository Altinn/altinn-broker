using System.Security.Claims;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Hosting;

using OneOf;

namespace Altinn.Broker.Application.ProvisionServiceOwner;

/// <summary>
/// Provisioning progress for the organization the end user acts for.
/// Requires publish on the BrokerBox configuration gatekeeper.
/// </summary>
public class GetServiceOwnerProvisioningHandler(
    IServiceOwnerRepository serviceOwnerRepository,
    IAuthorizationService authorizationService,
    IHostEnvironment hostEnvironment) : IHandler<string, ServiceOwnerProvisioningState>
{
    public async Task<OneOf<ServiceOwnerProvisioningState, Error>> Process(string request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var party = request.WithoutPrefix();
        if (!party.IsOrganizationNumber())
        {
            return Errors.InvalidParty;
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

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(party.WithPrefix());
        if (serviceOwner is null)
        {
            return ServiceOwnerProvisioningState.NotRegistered;
        }

        var phase = ServiceOwnerProvisioning.EffectivePhase(serviceOwner, hostEnvironment.IsDevelopment());
        if (phase == ProvisioningPhase.Ready && serviceOwner.ProvisioningPhase != ProvisioningPhase.Ready)
        {
            await serviceOwnerRepository.SetProvisioningPhase(serviceOwner.Id, ProvisioningPhase.Ready);
        }

        return ServiceOwnerProvisioningState.FromPhase(phase);
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
        catch (HttpRequestException)
        {
            return Errors.AuthorizationUnavailable;
        }
    }
}
