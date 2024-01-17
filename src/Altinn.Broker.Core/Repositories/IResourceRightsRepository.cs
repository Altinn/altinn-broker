namespace Altinn.Broker.Core.Repositories;
internal interface IResourceRightsRepository
{
    Task GiveUserAccess(string userId, string resourceId, string right);
    Task<bool> CheckUserAccess(string resourceId, string userId, string right);
    Task<bool> CheckOrganizationsAccess(List<string> organizationNumber);
}
