using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IAuthorizationService
{
    Task<bool> CheckUserAccess(string resourceId, List<ResourceAccessLevel> rights, bool IsLegacyUser = false, CancellationToken cancellationToken = default);
}
