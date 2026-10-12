using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetResource;

public class GetResourceHandler(
    IResourceRepository resourceRepository,
    BrokerResourceProvisioner resourceProvisioner) : IHandler<string, ResourceEntity>
{
    public async Task<OneOf<ResourceEntity, Error>> Process(string resourceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is not null)
        {
            return resource;
        }

        var (provisioned, outcome) = await resourceProvisioner.EnsureAsync(resourceId, cancellationToken);
        if (provisioned is not null)
        {
            return provisioned;
        }

        return outcome switch
        {
            BrokerResourceProvisionOutcome.ServiceOwnerNotConfigured => Errors.ServiceOwnerHasNotBeenConfigured,
            BrokerResourceProvisionOutcome.NotFoundInRegistry => Errors.InvalidResourceDefinition,
            _ => Errors.ResourceHasNotBeenConfigured
        };
    }
}
