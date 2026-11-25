namespace Altinn.Broker.Core.Services;

/// <summary>
/// Altinn's organization catalog. Membership means the organization can become a Broker service owner.
/// </summary>
public interface IAltinnOrganizationCatalog
{
    Task<bool> IsPossibleServiceOwner(string organizationNumber, CancellationToken cancellationToken);
}
