namespace Altinn.Broker.Helpers;

public static class AuthenticationSimulator
{
    public static string? GetCallerFromTestToken(HttpContext httpContext) => httpContext.User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
