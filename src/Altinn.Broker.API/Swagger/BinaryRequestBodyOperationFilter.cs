using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Broker.API.Swagger;

/// <summary>
/// Ensures endpoints that consume application/octet-stream expose a binary requestBody in OpenAPI.
/// </summary>
public sealed class BinaryRequestBodyOperationFilter : IOperationFilter
{
    private const string OctetStream = "application/octet-stream";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var consumesMediaTypes = context.ApiDescription.SupportedRequestFormats?
            .Select(f => f.MediaType)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m!.Trim().ToLowerInvariant())
            .ToList() ?? [];

        var hasOctetStreamViaApiDescription = consumesMediaTypes.Contains(OctetStream);

        var hasOctetStreamViaConsumesAttribute =
            context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<ConsumesAttribute>()
                .Any(a => a.ContentTypes.Any(ct => string.Equals(ct, OctetStream, StringComparison.OrdinalIgnoreCase)));

        if (!hasOctetStreamViaApiDescription && !hasOctetStreamViaConsumesAttribute) return;

        operation.RequestBody ??= new OpenApiRequestBody
        {
            Required = true
        };

        operation.RequestBody.Content[OctetStream] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            }
        };
    }
}


