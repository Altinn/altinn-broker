using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceRightsRepository
{
    Task GiveUserAccess(string userId, string resourceId, string right, string behalfOfOrganization);
    Task<bool> CheckUserAccess(string resourceId, string userId, ResourceAccessLevel right, bool IsLegacyUser = false);
    Task<bool> CheckOrganizationsHasAccess(List<string> organizationNumber);
}
