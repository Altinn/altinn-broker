using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Integrations.Altinn.Events.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly AltinnOptions _altinnOptions;
    private readonly HttpClient _httpClient;
    private readonly IPartyRepository _partyRepository;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ILogger<AltinnEventBus> _logger;

    public AltinnEventBus(HttpClient httpClient, IAltinnRegisterService altinnRegisterService, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnEventBus> logger, IPartyRepository partyRepository)
    {
        _httpClient = httpClient;
        _altinnOptions = altinnOptions.Value;
        _altinnRegisterService = altinnRegisterService;
        _partyRepository = partyRepository;
        _logger = logger;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? organizationId = null, CancellationToken cancellationToken = default)
    {
        string? partyId = null;
        if (organizationId != null)
        {
            var party = await _partyRepository.GetParty(organizationId, cancellationToken);
            if (party == null)
            {
                partyId = await _altinnRegisterService.LookUpOrganizationId(organizationId, cancellationToken);
                if (partyId != null) await _partyRepository.InitializeParty(organizationId, partyId);
            }
        }
        var cloudEvent = CreateCloudEvent(type, resourceId, fileTransferId, partyId, organizationId);
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new LowerCaseNamingPolicy()
        };
        var response = await _httpClient.PostAsync("events/api/v1/events", JsonContent.Create(cloudEvent, options: serializerOptions, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/cloudevents+json")), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unexpected null or invalid json response when posting cloud event {type} of {resourceId} with filetransfer id {fileTransferId}.", type, resourceId, fileTransferId);
            _logger.LogError("Statuscode was: {}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string fileTransferId, string? partyId, string? alternativeSubject)
    {
        CloudEvent cloudEvent = new CloudEvent()
        {
            Id = Guid.NewGuid(),
            SpecVersion = "1.0",
            Time = DateTime.UtcNow,
            Resource = "urn:altinn:resource:" + resourceId,
            ResourceInstance = fileTransferId,
            Type = "no.altinn.broker." + type.ToString().ToLowerInvariant(),
            Source = _altinnOptions.PlatformGatewayUrl + "broker/api/v1/filetransfer",
            Subject = !string.IsNullOrWhiteSpace(alternativeSubject) ? "/organisation/" + alternativeSubject : null,
            AlternativeSubject = !string.IsNullOrWhiteSpace(partyId) ? "/party/" + partyId : null,
        };

        return cloudEvent;
    }
}

