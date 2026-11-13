using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;


using Microsoft.Extensions.Logging;

using OneOf;

using System.Security.Claims;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Helpers;


namespace Altinn.Broker.Application.GetEligibleParties;

public class GetPartyEligibilityHandler(
    IEligiblePartyStore eligiblePartyStore,
    ILogger<GetPartyEligibilityHandler> logger) : IHandler<string, GetPartyEligibilityResponse>

{
    public async Task<OneOf<GetPartyEligibilityResponse, Error>> Process(string orgNumber, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogDebug("Checking eligibility for {orgNumber}", orgNumber.SanitizeForLogs());
        if (!orgNumber.IsOrganizationNumber())
        {
            return Errors.InvalidParty;
        }
        bool isEligible = await eligiblePartyStore.IsEligible(orgNumber, cancellationToken);

        return new GetPartyEligibilityResponse
        {
            IsEligible = isEligible
        };
    }
    
}