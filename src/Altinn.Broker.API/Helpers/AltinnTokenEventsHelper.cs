using Altinn.Broker.API.Models.Maskinporten;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.Helpers;

public class AltinnTokenEventsHelper
{
    public static Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        // Allow authentication failures to be handled by the default mechanism
        // This removes the blocking of Maskinporten tokens to support pure Maskinporten tokens
        return Task.CompletedTask;
    }

    public static async Task OnChallenge(JwtBearerChallengeContext context)
    {
        if (context.AuthenticateFailure != null && context.AuthenticateFailure is MaskinportenSecurityTokenException)
        {
            context.HandleResponse();
            context.Response.Headers.Append("WWW-Authenticate", context.Options.Challenge + " error=\"invalid_token\"");
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails()
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "IDX10205: Issuer validation failed",
                Detail = "Maskinporten token is not valid. Exchange to Altinn token and try again. Read more at https://docs.altinn.studio/api/scenarios/authentication/#maskinporten-jwt-access-token-input"
            });
        }
    }
}
