using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Integrations.Altinn.Events.Helpers;

using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly AltinnOptions _altinnOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnEventBus> _logger;

    public AltinnEventBus(
        HttpClient httpClient,
        IOptions<AltinnOptions> altinnOptions,
        ILogger<AltinnEventBus> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(altinnOptions.Value.PlatformGatewayUrl);
        _logger = logger;
        _altinnOptions = altinnOptions.Value;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string subjectOrganizationNumber, EventSubjectType eventSubjectType, Guid eventIdempotencyKey, CancellationToken cancellationToken = default)
    {
        await Publish(type, resourceId, fileTransferId, eventIdempotencyKey, DateTime.UtcNow, subjectOrganizationNumber, eventSubjectType, cancellationToken);
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, Guid eventId, DateTime time, string subjectOrganizationNumber, EventSubjectType eventSubjectType, CancellationToken cancellationToken = default)
    {
        var cloudEvent = CreateCloudEvent(type, resourceId, fileTransferId, subjectOrganizationNumber, eventSubjectType, eventId, time);
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new LowerCaseNamingPolicy()
        };
        var requestBody = JsonContent.Create(cloudEvent, options: serializerOptions, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/cloudevents+json"));
        var response = await _httpClient.PostAsync("events/api/v1/events", requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unexpected null or invalid json response when posting cloud event {type} of {resourceId} with filetransfer id {fileTransferId}.", type, resourceId, fileTransferId);
            _logger.LogError("Statuscode was: {}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string fileTransferId, string organizationNumber, EventSubjectType eventSubjectType, Guid eventId, DateTime time)
    {
        if (organizationNumber.Contains(":"))
        {
            organizationNumber = organizationNumber.WithoutPrefix();
        }
        CloudEvent cloudEvent = new CloudEvent()
        {
            Id = eventId,
            SpecVersion = "1.0",
            Time = time,
            Resource = "urn:altinn:resource:" + resourceId,
            ResourceInstance = fileTransferId,
            Type = "no.altinn.broker." + type.ToString().ToLowerInvariant(),
            Data = new Dictionary<string, object>
            {
                { "role", eventSubjectType.ToString().ToLowerInvariant() }
            },
            Source = _altinnOptions.PlatformGatewayUrl + "broker/api/v1/filetransfer",
            Subject = organizationNumber.WithPrefix()
        };

        return cloudEvent;
    }
}

