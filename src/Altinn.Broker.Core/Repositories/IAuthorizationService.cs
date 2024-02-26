using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IAuthorizationService
{
    Task<bool> CheckUserAccess(string resourceId, string userId, List<ResourceAccessLevel> rights, bool IsLegacyUser = false, CancellationToken cancellationToken = default);
}
