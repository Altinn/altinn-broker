namespace Altinn.Broker.Core.Services;
public interface IAltinnRegisterService
{
    Task<string?> LookUpOrganizationId(string organizationId, CancellationToken cancellationToken);
}
