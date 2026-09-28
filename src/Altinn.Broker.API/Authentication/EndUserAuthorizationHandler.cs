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

/// <summary>
/// Coarse gate for ConfigureResource: service-owner scope <em>or</em> an authenticated
/// Broker end-user session. The handler still enforces org ownership or PDP <c>publish</c>.
/// </summary>
public sealed class ConfigureResourceAccessRequirement : IAuthorizationRequirement { }

public sealed class ConfigureResourceAccessHandler : AuthorizationHandler<ConfigureResourceAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ConfigureResourceAccessRequirement requirement)
    {
        if (HasServiceOwnerScope(context.User) || IsBrokerEndUser(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasServiceOwnerScope(System.Security.Claims.ClaimsPrincipal user)
    {
        return user.Claims.Any(claim =>
            (claim.Type is "scope" or "scp" or "urn:altinn:scope")
            && claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(AuthorizationConstants.ServiceOwnerScope));
    }

    private static bool IsBrokerEndUser(System.Security.Claims.ClaimsPrincipal user)
    {
        var isCookieAuthenticated = user.Identities.Any(identity =>
            identity.IsAuthenticated
            && identity.AuthenticationType is AuthorizationConstants.EndUserCookie
                or AuthorizationConstants.AltinnPlatformJwtCookie);
        var hasAltinnEndUserIdentity = user.HasClaim(claim =>
            claim.Type is "urn:altinn:userid" or "urn:altinn:partyid");
        return isCookieAuthenticated && hasAltinnEndUserIdentity;
    }
}
