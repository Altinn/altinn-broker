using System.Security.Claims;

using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IAuthorizationService
{
    Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string party, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsSenderOrRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessForSearch(ClaimsPrincipal? user, string resourceId, string party, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks PDP, in one multi-decision request, which of the given resources the user can send
    /// and/or receive file transfers on for the given party. Resources the user has no access to
    /// are returned with both access flags false.
    /// </summary>
    Task<List<AuthorizedResource>> GetAuthorizedResources(ClaimsPrincipal? user, string party, IReadOnlyList<string> resourceIds, CancellationToken cancellationToken = default);
}
