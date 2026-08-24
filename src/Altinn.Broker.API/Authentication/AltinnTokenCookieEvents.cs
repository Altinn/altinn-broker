using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Integrations.Altinn;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Altinn.Broker.API.Authentication;

/// <summary>
/// On each request authenticated via cookie, validates the stored Altinn token.
/// If expired, attempts re-exchange using the stored ID-Porten access/refresh material.
/// Sets ClaimsPrincipal from the Altinn token so downstream authorization sees urn:altinn:* claims.
/// </summary>
public class AltinnTokenCookieEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var tokens = context.Properties.GetTokens().ToList();
        var altinnToken = tokens.FirstOrDefault(t => t.Name == "altinn_token")?.Value;

        if (string.IsNullOrEmpty(altinnToken) || !CanRead(altinnToken, out var jwt))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthorizationConstants.EndUserCookie);
            return;
        }

        if (jwt!.ValidTo < DateTime.UtcNow)
        {
            var refreshed = await TryReExchange(context, tokens);
            if (!refreshed)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(AuthorizationConstants.EndUserCookie);
                return;
            }

            altinnToken = context.Properties.GetTokenValue("altinn_token");
            if (string.IsNullOrEmpty(altinnToken) || !CanRead(altinnToken, out jwt))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(AuthorizationConstants.EndUserCookie);
                return;
            }
        }

        var identity = new ClaimsIdentity(
            jwt.Claims,
            AuthorizationConstants.EndUserCookie,
            ClaimTypes.Name,
            ClaimTypes.Role);
        context.ReplacePrincipal(new ClaimsPrincipal(identity));
        context.ShouldRenew = false;
    }

    private static bool CanRead(string token, out JwtSecurityToken? jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            jwt = null;
            return false;
        }

        jwt = handler.ReadJwtToken(token);
        return true;
    }

    private static async Task<bool> TryReExchange(CookieValidatePrincipalContext context, List<AuthenticationToken> tokens)
    {
        // Legacy cookies may still carry an ID-Porten access token.
        var idPortenAccessToken = tokens.FirstOrDefault(t => t.Name == "id_porten_access_token")?.Value;
        if (string.IsNullOrEmpty(idPortenAccessToken))
        {
            return false;
        }

        var tokenExchange = context.HttpContext.RequestServices.GetRequiredService<IAltinnTokenExchangeService>();
        var newAltinnToken = await tokenExchange.ExchangeIdPortenToken(idPortenAccessToken);
        if (string.IsNullOrEmpty(newAltinnToken))
        {
            return false;
        }

        var updatedTokens = tokens.Select(t => t.Name == "altinn_token"
            ? new AuthenticationToken { Name = "altinn_token", Value = newAltinnToken }
            : t).ToList();
        context.Properties.StoreTokens(updatedTokens);
        context.ShouldRenew = true;
        return true;
    }
}
