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
            try
            {
                var systemUserAuthorizationDetails = JsonSerializer.Deserialize<SystemUserAuthorizationDetails>(systemUserClaim.Value);
                return systemUserAuthorizationDetails?.SystemUserOrg.ID.WithoutPrefix();
            }
            catch (JsonException)
            {
                // Invalid JSON in authorization_details claim
                return null;
            }
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
            try
            {
                var consumerObject = JsonSerializer.Deserialize<TokenConsumer>(consumerClaim.Value);
                return consumerObject?.ID?.WithoutPrefix();
            }
            catch (JsonException)
            {
                return null;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Returns the vendor that authenticated to Maskinporten,
    /// taken from the consumer claim. Differs from <see cref="GetCallerOrganizationId"/> for
    /// system-user tokens where a vendor authenticates on behalf of an end-user org; equal to it
    /// for self-acting flows. Returns null when no consumer claim is present.
    /// </summary>
    public static string? GetCallerVendorId(this ClaimsPrincipal user)
    {
        var consumerClaim = user.Claims.FirstOrDefault(c => c.Type == "consumer");
        if (consumerClaim is null)
        {
            return null;
        }

        try
        {
            var consumer = JsonSerializer.Deserialize<TokenConsumer>(consumerClaim.Value);
            var vendorId = consumer?.ID?.WithoutPrefix();
            return string.IsNullOrWhiteSpace(vendorId) ? null : vendorId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
