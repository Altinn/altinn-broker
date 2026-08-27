using System.Security.Claims;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Keeps the minimum set of claims from the validated ID-Porten identity that
/// downstream PDP mapping needs after the Altinn token exchange.
/// </summary>
internal static class IdPortenPrincipalClaims
{
    internal const string AuthenticationType = "IdPorten";
    internal const string IssuerClaim = "iss";
    internal const string PersonIdentifierClaim = "pid";
    internal const string AuthenticationContextClaim = "acr";
    internal const string MappedAuthenticationContextClaim = "http://schemas.microsoft.com/claims/authnclassreference";

    private static readonly HashSet<string> PreservedClaimTypes =
    [
        IssuerClaim,
        PersonIdentifierClaim,
        AuthenticationContextClaim,
        MappedAuthenticationContextClaim
    ];

    internal static ClaimsIdentity CreateIdentity(ClaimsPrincipal principal, string? validatedIssuer = null)
    {
        var claims = principal.Claims
            .Where(claim => PreservedClaimTypes.Contains(claim.Type))
            .Select(claim => claim.Clone())
            .ToList();

        if (!claims.Any(claim => claim.Type == IssuerClaim)
            && !string.IsNullOrWhiteSpace(validatedIssuer))
        {
            claims.Add(new Claim(
                IssuerClaim,
                validatedIssuer,
                ClaimValueTypes.String,
                validatedIssuer));
        }

        return new ClaimsIdentity(claims, AuthenticationType);
    }

    internal static ClaimsIdentity? CopyIdentity(ClaimsPrincipal? principal)
    {
        var identity = principal?.Identities.FirstOrDefault(candidate =>
            candidate.AuthenticationType == AuthenticationType);

        return identity is null
            ? null
            : new ClaimsIdentity(identity.Claims.Select(claim => claim.Clone()), AuthenticationType);
    }
}
