using System.Net;
using System.Text.Json;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Models.Maskinporten;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Helpers;

public static class ClaimHelper
{
    public static string? GetCallerFromTestToken(HttpContext httpContext) => httpContext.User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public static string? GetConsumerFromToken(HttpContext httpContext)
    {
        var consumerClaim = httpContext.User.Claims.FirstOrDefault(claim => claim.Type == "consumer");
        if (consumerClaim is null)
        {
            return null;
        }
        var consumer = JsonSerializer.Deserialize<MaskinportenConsumer>(consumerClaim.Value);
        return consumer?.ID;
    }

    public static string? GetSupplierFromToken(HttpContext httpContext)
    {
        var supplierClaim = httpContext.User.Claims.FirstOrDefault(claim => claim.Type == "supplier");
        if (supplierClaim is null)
        {
            return null;
        }
        var supplier = JsonSerializer.Deserialize<MaskinportenSupplier>(supplierClaim.Value);
        return supplier?.ID;
    }

    public static string? GetScopeFromToken(HttpContext httpContext)
    {
        var scopeClaim = httpContext.User.Claims.FirstOrDefault(claim => claim.Type == "scope");
        if (scopeClaim is null)
        {
            return null;
        }
        return scopeClaim.Value;

    }
}
