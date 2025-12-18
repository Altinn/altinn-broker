using System.Diagnostics;
using System.Net;
using Altinn.Authorization.ProblemDetails;
using Altinn.Broker.Application;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.API.Helpers;

public static class ProblemDetailsHelper
{
    private static readonly ProblemDescriptorFactory _factory = ProblemDescriptorFactory.New("BRO");

    private static readonly Dictionary<HttpStatusCode, (string Type, string Title)> StatusCodeMappings = new()
    {
        { HttpStatusCode.BadRequest, ("https://tools.ietf.org/html/rfc9110#section-15.5.1", "Bad Request") },
        { HttpStatusCode.Unauthorized, ("https://tools.ietf.org/html/rfc9110#section-15.5.2", "Unauthorized") },
        { HttpStatusCode.Forbidden, ("https://tools.ietf.org/html/rfc9110#section-15.5.4", "Forbidden") },
        { HttpStatusCode.NotFound, ("https://tools.ietf.org/html/rfc9110#section-15.5.5", "Not Found") },
        { HttpStatusCode.Conflict, ("https://tools.ietf.org/html/rfc9110#section-15.5.10", "Conflict") },
        { HttpStatusCode.InternalServerError, ("https://tools.ietf.org/html/rfc9110#section-15.6.1", "Internal Server Error") },
    };

    public static ObjectResult ToProblemResult(Error error)
    {
        var descriptor = _factory.Create((uint)error.ErrorCode, error.StatusCode, error.Message);
        var problemDetails = descriptor.ToProblemDetails();

        if (StatusCodeMappings.TryGetValue(error.StatusCode, out var mapping))
        {
            problemDetails.Type = mapping.Type;
            problemDetails.Title = mapping.Title;
        }

        var traceId = Activity.Current?.Id;
        if (!string.IsNullOrEmpty(traceId))
        {
            problemDetails.Extensions["traceId"] = traceId;
        }
        
        return new ObjectResult(problemDetails)
        {
            StatusCode = (int)error.StatusCode
        };
    }
}
