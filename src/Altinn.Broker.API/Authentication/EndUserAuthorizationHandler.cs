using Altinn.Broker.API.Configuration;

using Altinn.Common.PEP.Authorization;

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

/// <summary>
/// Allows authenticated Broker end users through the API's scope gate. The
/// application handler still performs the resource and party check against PDP.
/// </summary>
public sealed class EndUserScopeAccessHandler : AuthorizationHandler<ScopeAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeAccessRequirement requirement)
    {
        var isCookieAuthenticated = context.User.Identities.Any(identity =>
            identity.IsAuthenticated
            && identity.AuthenticationType is AuthorizationConstants.EndUserCookie
                or AuthorizationConstants.AltinnPlatformJwtCookie);
        var hasAltinnEndUserIdentity = context.User.HasClaim(claim =>
            claim.Type is "urn:altinn:userid" or "urn:altinn:partyid");
        var isBrokerEndUserScope = requirement.Scope.Any(scope =>
            scope is AuthorizationConstants.SenderScope or AuthorizationConstants.RecipientScope);

        if (isCookieAuthenticated && hasAltinnEndUserIdentity && isBrokerEndUserScope)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
