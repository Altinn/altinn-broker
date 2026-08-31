using Microsoft.AspNetCore.Authorization;

namespace Altinn.Broker.API.Authentication;

public class EndUserRequirement : IAuthorizationRequirement { }

/// <summary>
/// Validates that the authenticated end-user has the expected Altinn claims
/// (urn:altinn:userid or urn:altinn:partyid) from the exchanged token.
/// </summary>
public class EndUserAuthorizationHandler : AuthorizationHandler<EndUserRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EndUserRequirement requirement)
    {
        var hasUserId = context.User.HasClaim(c => c.Type == "urn:altinn:userid");
        var hasPartyId = context.User.HasClaim(c => c.Type == "urn:altinn:partyid");

        if (hasUserId || hasPartyId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
