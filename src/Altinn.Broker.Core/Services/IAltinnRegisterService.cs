using Altinn.Platform.Register.Models;

namespace Altinn.Broker.Core.Services;
public interface IAltinnRegisterService
{
    /// <summary>
    /// Looks up a party by its organization number.
    /// </summary>
    /// <returns>The party, or <see langword="null"/> when it is unknown to Altinn Register.</returns>
    Task<Party?> LookupPartyByOrganizationNumber(string organizationNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a party by its party uuid.
    /// </summary>
    /// <returns>The party, or <see langword="null"/> when it is unknown to Altinn Register.</returns>
    Task<Party?> LookupPartyByUuid(string partyUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the name of an organization by its organization number.
    /// </summary>
    /// <returns>The name, or <see langword="null"/> when it is unknown to Altinn Register or could not be looked up.</returns>
    Task<string?> LookupOrganizationName(string organizationId, CancellationToken cancellationToken = default);
}
