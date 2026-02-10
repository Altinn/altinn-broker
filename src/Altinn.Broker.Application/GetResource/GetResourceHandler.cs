using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Exceptions;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetResource;
public class GetResourceHandler(IResourceRepository resourceRepository) : IHandler<string, ResourceEntity>
{
    public async Task<OneOf<ResourceEntity, Error>> Process(string resourceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var resource = null as ResourceEntity;
        try{
        resource = await resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.NoAccessToResource;
        }
        }catch (ServiceOwnerNotConfiguredException)
        {
            return Errors.ServiceOwnerHasNotBeenConfigured;
        }
        var serviceOwner = user.GetCallerOrganizationId();
        if (resource.OrganizationNumber.WithoutPrefix() != user.GetCallerOrganizationId().WithoutPrefix())
        {
            return Errors.NoAccessToResource;
        }

        return resource;
    }
}
