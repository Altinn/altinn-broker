using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetResource;
public class GetResourceHandler(IResourceRepository resourceRepository) : IHandler<string, ResourceEntity>
{
    public async Task<OneOf<ResourceEntity, Error>> Process(string resourceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.ResourceHasNotBeenConfigured;
        };
        
        var callerOrganizationId = user?.GetCallerOrganizationId()?.WithoutPrefix();
        var resourceOrgNumber = resource.OrganizationNumber?.WithoutPrefix();
        if (resourceOrgNumber == null || callerOrganizationId == null || resourceOrgNumber != callerOrganizationId)
        {
            return Errors.NoAccessToResource;
        }
        return resource;
    }
}
