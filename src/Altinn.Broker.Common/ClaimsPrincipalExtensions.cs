using System.Security.Claims;
using System.Text.Json;

using Altinn.Broker.Common.Helpers.Models;

namespace Altinn.Broker.Common;
public static class ClaimsPrincipalExtensions
{
    public static string? GetCallerOrganizationId(this ClaimsPrincipal user)
    {
        var claims = user.Claims;
        
        // System user token (from Maskinporten with authorization_details)
        var systemUserClaim = user.Claims.FirstOrDefault(c => c.Type == "authorization_details");
        if (systemUserClaim is not null)
        {
            var systemUserAuthorizationDetails = JsonSerializer.Deserialize<SystemUserAuthorizationDetails>(systemUserClaim.Value);
            return systemUserAuthorizationDetails?.SystemUserOrg.ID.WithoutPrefix();
        }
        
        // Enterprise token (from Altinn)
        var orgClaim = user.Claims.FirstOrDefault(c => c.Type == "urn:altinn:orgNumber");
        if (orgClaim is not null)
        {
            return orgClaim.Value.WithoutPrefix(); // Normalize to same format as elsewhere
        }
        
        // Legacy Maskinporten token with consumer claim
        var consumerClaim = user.Claims.FirstOrDefault(c => c.Type == "consumer");
        if (consumerClaim is not null)
        {
            var consumerObject = JsonSerializer.Deserialize<TokenConsumer>(consumerClaim.Value);
            return consumerObject.ID.WithoutPrefix();
        }
        
        return null;
    }

}
