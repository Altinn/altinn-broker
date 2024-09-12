using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Integrations.Altinn.Events.Helpers;

using Hangfire;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class AltinnEventBus(
    HttpClient httpClient, 
    IAltinnRegisterService altinnRegisterService, 
    IBackgroundJobClient backgroundJobClient,
    IPartyRepository partyRepository,
    IOptions<AltinnOptions> altinnOptions, 
    ILogger<AltinnEventBus> logger) : IEventBus
{
    private readonly AltinnOptions _altinnOptions = altinnOptions.Value;

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? subjectOrganizationNumber = null, CancellationToken cancellationToken = default)
    {
        backgroundJobClient.Enqueue(() => Publish(type, resourceId, fileTransferId, Guid.NewGuid(), DateTime.UtcNow, subjectOrganizationNumber, cancellationToken));
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, Guid eventId, DateTime time, string? subjectOrganizationNumber = null, CancellationToken cancellationToken = default)
    {
        string? partyId = null;
        if (subjectOrganizationNumber != null)
        {
            var party = await partyRepository.GetParty(subjectOrganizationNumber, cancellationToken);
            if (party == null)
            {
                partyId = await altinnRegisterService.LookUpOrganizationId(subjectOrganizationNumber, cancellationToken);
                if (partyId != null) await partyRepository.InitializeParty(subjectOrganizationNumber, partyId);
            }
        }
        var cloudEvent = CreateCloudEvent(type, resourceId, fileTransferId, partyId, subjectOrganizationNumber, eventId, time);
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new LowerCaseNamingPolicy()
        };
        var requestBody = JsonContent.Create(cloudEvent, options: serializerOptions, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/cloudevents+json"));
        var response = await httpClient.PostAsync("events/api/v1/events", requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Unexpected null or invalid json response when posting cloud event {type} of {resourceId} with filetransfer id {fileTransferId}.", type, resourceId, fileTransferId);
            logger.LogError("Statuscode was: {}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string fileTransferId, string? partyId, string? organizationNumber, Guid? eventId, DateTime time)
    {
        if (organizationNumber is not null && organizationNumber.StartsWith("0192:"))
        {
            organizationNumber = organizationNumber.Split(":")[1];
        }
        CloudEvent cloudEvent = new CloudEvent()
        {
            Id = eventId ?? Guid.NewGuid(),
            SpecVersion = "1.0",
            Time = time,
            Resource = "urn:altinn:resource:" + resourceId,
            ResourceInstance = fileTransferId,
            Type = "no.altinn.broker." + type.ToString().ToLowerInvariant(),
            Source = _altinnOptions.PlatformGatewayUrl + "broker/api/v1/filetransfer",
            Subject = !string.IsNullOrWhiteSpace(organizationNumber) ? "/organisation/" + organizationNumber : null,
            AlternativeSubject = !string.IsNullOrWhiteSpace(partyId) ? "/party/" + partyId : null,
        };

        return cloudEvent;
    }
}

