namespace Altinn.Broker.Core.Services;
public interface IAltinnRegisterService
{
    Task<string?> LookUpOrganizationId(string organizationId, CancellationToken cancellationToken);
    Task<string?> LookupPartyByUuid(string partyUuid, CancellationToken cancellationToken);
    Task<string?> LookupOrganizationName(string organizationId, CancellationToken cancellationToken);
}
