using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.Helpers;
public class JWTBearerEventsHelper
{
    public static Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        context.Response.Headers.Append("WWW-Authenticate", context.Options.Challenge + " error=\"invalid_token\"");
        string err = "";
        if (context.Exception is SecurityTokenInvalidIssuerException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var issuer = ((SecurityTokenInvalidIssuerException)context.Exception).InvalidIssuer;
            if (issuer.ToString().Contains("maskinporten.no"))
            {
                err = "IDX10205: Issuer validation failed. Maskinporten token is not valid. Exchange to Altinn token and try again. Read more at https://docs.altinn.studio/api/scenarios/authentication/#maskinporten-jwt-access-token-input";
            }
        }

        return context.Response.WriteAsync(err);
    }
}
