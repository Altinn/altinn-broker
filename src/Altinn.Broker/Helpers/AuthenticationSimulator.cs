using System.Net;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Helpers;

public static class AuthenticationSimulator
{
    public static string? GetCallerFromTestToken(HttpContext httpContext) => httpContext.User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public static async Task<(ServiceOwnerEntity? serviceOwner, ObjectResult? actionResult)> AuthenticateRequestAsync(HttpContext httpContext, IServiceOwnerRepository serviceOwnerRepository)
    {
        var caller = AuthenticationSimulator.GetCallerFromTestToken(httpContext);
        if (string.IsNullOrWhiteSpace(caller))
        {
            return (null, new ObjectResult("You need to pass in a JWT token with a sub claim")
            {
                StatusCode = (int)HttpStatusCode.Unauthorized
            });
        }
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(caller);
        if (serviceOwner is null)
        {
            return (null, new ObjectResult($"Service owner {caller} has not been setup for the broker service.")
            {
                StatusCode = (int)HttpStatusCode.Unauthorized
            });
        }
        if (serviceOwner.StorageProvider is null)
        {
            return (null, new ObjectResult($"Service owner infrastructure is not ready.")
            {
                StatusCode = (int)HttpStatusCode.ServiceUnavailable
            });
        }
        return (serviceOwner, null);
    }
}
