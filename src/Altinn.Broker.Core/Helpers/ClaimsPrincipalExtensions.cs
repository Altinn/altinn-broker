using System.Security.Claims;
using System.Text.Json;

using Altinn.Broker.Core.Helpers.Models;

namespace Altinn.Broker.Core.Helpers;
public static class ClaimsPrincipalExtensions
{
    public static string? GetCallerOrganizationId(this ClaimsPrincipal user)
    {
        var claims = user.Claims;
        // System user token
        var systemUserClaim = user.Claims.FirstOrDefault(c => c.Type == "authorization_details");
        if (systemUserClaim is not null)
        {
            var systemUserAuthorizationDetails = JsonSerializer.Deserialize<SystemUserAuthorizationDetails>(systemUserClaim.Value);
            return systemUserAuthorizationDetails?.SystemUserOrg.ID.Replace("0192:", "");
        }
        // Enterprise token
        var orgClaim = user.Claims.FirstOrDefault(c => c.Type == "urn:altinn:orgNumber");
        if (orgClaim is not null)
        {
            return orgClaim.Value.Replace("0192:", ""); // Normalize to same format as elsewhere
        }
        var consumerClaim = user.Claims.FirstOrDefault(c => c.Type == "consumer");
        if (consumerClaim is not null)
        {
            var consumerObject = JsonSerializer.Deserialize<TokenConsumer>(consumerClaim.Value);
            return consumerObject.ID.Replace("0192:", "");
        }
        return null;
    }

}
