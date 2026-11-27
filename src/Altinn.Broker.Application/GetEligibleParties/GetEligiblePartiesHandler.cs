using System.Security.Claims;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetEligibleParties;

public class GetEligiblePartiesHandler(
    IEligiblePartyStore eligiblePartyStore,
    ILogger<GetEligiblePartiesHandler> logger) : IHandler<GetEligiblePartiesResponse>
{
    public async Task<OneOf<GetEligiblePartiesResponse, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting all eligible parties for broker");
        var eligibleParties = await eligiblePartyStore.GetAll(cancellationToken);
        return new GetEligiblePartiesResponse { EligibleParties = eligibleParties };
    }
}