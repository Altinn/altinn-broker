using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.Helpers;
public class JWTBearerEventsHelper
{
    public static Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        if (context.Exception is SecurityTokenInvalidIssuerException)
        {
            var issuer = ((SecurityTokenInvalidIssuerException)context.Exception).InvalidIssuer;
            if (issuer.ToString().Contains("maskinporten.no"))
            {
                context.Response.ContentType = "application/problem+json";
                context.Response.Headers.Append("WWW-Authenticate", context.Options.Challenge + " error=\"invalid_token\"");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(new ProblemDetails()
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "IDX10205: Issuer validation failed",
                    Detail = "Maskinporten token is not valid. Exchange to Altinn token and try again. Read more at https://docs.altinn.studio/api/scenarios/authentication/#maskinporten-jwt-access-token-input"
                });
            }
        }
        return Task.CompletedTask;
    }
}
