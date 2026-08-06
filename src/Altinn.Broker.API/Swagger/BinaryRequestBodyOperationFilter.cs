using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

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

        var requestBody = operation.RequestBody as OpenApiRequestBody ?? new OpenApiRequestBody();
        requestBody.Required = true;
        requestBody.Content ??= new Dictionary<string, OpenApiMediaType>();
        operation.RequestBody = requestBody;

        requestBody.Content[OctetStream] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary"
            }
        };
    }
}

