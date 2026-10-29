using System.Security.Claims;

using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IAuthorizationService
{
    /// <param name="requireRegisteredResource">
    /// When true (default), deny if the resource is not yet stored in Broker.
    /// Set false for flows that may auto-register a BrokerService resource (e.g. Initialize).
    /// </param>
    Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string party, CancellationToken cancellationToken = default, bool requireRegisteredResource = true);
    Task<bool> CheckAccessAsSenderOrRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessForSearch(ClaimsPrincipal? user, string resourceId, string party, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, CancellationToken cancellationToken = default);
    /// <summary>
    /// PDP check for the <c>publish</c> action on the resource for the given party (used when configuring a resource).
    /// </summary>
    Task<bool> CheckAccessAsPublisher(ClaimsPrincipal? user, string resourceId, string party, CancellationToken cancellationToken = default);
    Task<bool> IsIdPortenToken(ClaimsPrincipal? user);
    Task<bool> CheckIdPortenAccessAsRecipient(ClaimsPrincipal? user, FileTransferEntity fileTransfer, string onBehalfOf, CancellationToken cancellationToken = default);

    Task<List<AuthorizedResource>> GetAuthorizedResources(ClaimsPrincipal? user, string party, IReadOnlyList<string> resourceIds, CancellationToken cancellationToken = default);
}
