using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Broker.Common;
using Altinn.Broker.Common.Constants;
using Altinn.Common.PEP.Helpers;

namespace Altinn.Broker.Integrations.Altinn.Authorization;

internal static class IdportenXacmlMapper
{
    internal const string AuthenticationContextClaim = "acr";
    internal const string MappedAuthenticationContextClaim = "http://schemas.microsoft.com/claims/authnclassreference";

    private const string IdportenHost = "idporten.no";
    private const string TestIdportenHost = "test.idporten.no";
    private const string DefaultType = "string";

    internal static bool IsIdportenToken(ClaimsPrincipal user)
        => FindIdportenIdentity(user) is not null;

    internal static bool TryCreateSubjectCategory(ClaimsPrincipal user, out XacmlJsonCategory subjectCategory)
    {
        subjectCategory = new XacmlJsonCategory { Attribute = [] };

        var idportenIdentity = FindIdportenIdentity(user);
        var pidClaim = idportenIdentity?.FindFirst("pid");
        if (string.IsNullOrWhiteSpace(pidClaim?.Value))
        {
            return false;
        }

        subjectCategory.Attribute.Add(
            DecisionHelper.CreateXacmlJsonAttribute(
                UrnConstants.PersonIdAttribute,
                pidClaim.Value.WithoutPrefix(),
                DefaultType,
                pidClaim.Issuer));

        return true;
    }

    internal static bool ValidateAuthorizationResponse(XacmlJsonResponse response, ClaimsPrincipal user)
    {
        if (response.Response is null || response.Response.Count == 0)
        {
            return false;
        }

        foreach (var result in response.Response)
        {
            if (!string.Equals(result.Decision, XacmlContextDecision.Permit.ToString(), StringComparison.Ordinal))
            {
                return false;
            }

            if (!MeetsAuthenticationLevelObligations(result, user))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MeetsAuthenticationLevelObligations(XacmlJsonResult result, ClaimsPrincipal user)
    {
        var minimumAuthenticationLevels = result.Obligations?
            .SelectMany(obligation => obligation.AttributeAssignment)
            .Where(assignment => string.Equals(
                assignment.Category,
                UrnConstants.MinimumAuthenticationLevel,
                StringComparison.Ordinal))
            .Select(assignment => assignment.Value)
            .ToList();

        if (minimumAuthenticationLevels is null || minimumAuthenticationLevels.Count == 0)
        {
            return true;
        }

        if (!TryGetAuthenticationLevel(user, out var authenticationLevel))
        {
            return false;
        }

        return minimumAuthenticationLevels.All(minimumLevel =>
            int.TryParse(minimumLevel, out var parsedMinimumLevel)
            && authenticationLevel >= parsedMinimumLevel);
    }

    private static bool TryGetAuthenticationLevel(ClaimsPrincipal user, out int authenticationLevel)
    {
        var idportenIdentity = FindIdportenIdentity(user);
        var authenticationContext = idportenIdentity?.FindFirst(AuthenticationContextClaim)?.Value
            ?? idportenIdentity?.FindFirst(MappedAuthenticationContextClaim)?.Value;

        authenticationLevel = authenticationContext switch
        {
            "idporten-loa-high" => 4,
            "idporten-loa-substantial" => 3,
            "idporten-loa-low" => 2,
            "selfregistered-email" => 0,
            _ => -1
        };

        return authenticationLevel >= 0;
    }

    private static ClaimsIdentity? FindIdportenIdentity(ClaimsPrincipal user)
        => user.Identities.FirstOrDefault(identity =>
            identity.FindAll("iss").Any(claim => IsIdportenIssuer(claim.Value)));

    private static bool IsIdportenIssuer(string issuer)
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || issuerUri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return issuerUri.Host.Equals(IdportenHost, StringComparison.OrdinalIgnoreCase)
            || issuerUri.Host.Equals(TestIdportenHost, StringComparison.OrdinalIgnoreCase);
    }
}
