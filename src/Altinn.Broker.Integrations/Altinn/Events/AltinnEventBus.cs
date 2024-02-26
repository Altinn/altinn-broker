using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Integrations.Altinn.Events.Helpers;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnEventBus> _logger;

    public AltinnEventBus(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IOptions<AltinnOptions> options, ILogger<AltinnEventBus> logger)
    {
        httpClient.BaseAddress = new Uri(options.Value.PlatformGatewayUrl);
        httpClient.DefaultRequestHeaders.Add("Authorization", httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString());
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileId)
    {
        if (_httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == "scope" && c.Value == "altinn:events.publish") ?? false)
        {
            _logger.LogInformation("Skipping event publish because token does not include the scope \"altinn:events.publish\"");
            return;
        }
        var cloudEvent = CreateCloudEvent(type, resourceId, fileId);
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new LowerCaseNamingPolicy()
        };
        var response = await _httpClient.PostAsync("events/api/v1/events", JsonContent.Create(cloudEvent, options: serializerOptions, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/cloudevents+json")));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unexpected null or invalid json response when posting cloud event {type} of {resourceId} with file id {fileId}.", type, resourceId, fileId);
            _logger.LogError("Statuscode was: {}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string fileId)
    {
        CloudEvent cloudEvent = new CloudEvent()
        {
            Id = Guid.NewGuid(),
            SpecVersion = "1.0",
            Time = DateTime.UtcNow,
            Resource = "urn:altinn:resource:" + resourceId,
            ResourceInstance = fileId,
            Type = "no.altinn.broker." + type.ToString().ToLowerInvariant(),
            Source = _httpContextAccessor.HttpContext?.Request.PathBase.Value + _httpContextAccessor.HttpContext?.Request.Path.Value
        };

        return cloudEvent;
    }
}

