using Altinn.Broker.API.Models.Maskinporten;
using Altinn.Broker.API.Tus;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.Helpers;

public class AltinnTokenEventsHelper
{
    public static async Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        if (!IsExpiredTokenFailure(context.Exception))
        {
            return;
        }

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AltinnTokenEventsHelper));

        var sessionHelper = context.HttpContext.RequestServices.GetService<TusUploadSessionAuthenticationHelper>();
        if (sessionHelper is null)
        {
            logger.LogWarning(
                "Expired JWT authentication failed and TUS session helper is unavailable. Scheme={Scheme} Path={Path}",
                context.Scheme.Name,
                context.HttpContext.Request.Path.Value);
            return;
        }

        var principal = await sessionHelper.TryValidateExpiredTokenForActiveUploadAsync(
            context.HttpContext,
            context.HttpContext.RequestAborted);
        if (principal is null)
        {
            logger.LogWarning(
                "Expired JWT authentication failed and no active TUS upload session was found. Scheme={Scheme} Path={Path}",
                context.Scheme.Name,
                context.HttpContext.Request.Path.Value);
            return;
        }

        context.Principal = principal;
        context.Success();

        logger.LogInformation(
            "Recovered expired JWT via OnAuthenticationFailed for active TUS upload. Scheme={Scheme} Path={Path}",
            context.Scheme.Name,
            context.HttpContext.Request.Path.Value);
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

    private static bool IsExpiredTokenFailure(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SecurityTokenExpiredException)
            {
                return true;
            }
        }

        return false;
    }
}
