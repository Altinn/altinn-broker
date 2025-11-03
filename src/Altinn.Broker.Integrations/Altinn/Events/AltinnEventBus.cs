using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Integrations.Altinn.Events.Helpers;

using Hangfire;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Events;
public class AltinnEventBus : IEventBus
{
    private readonly AltinnOptions _altinnOptions;
    private readonly HttpClient _httpClient;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IPartyRepository _partyRepository;
    private readonly ILogger<AltinnEventBus> _logger;

    public AltinnEventBus(
        HttpClient httpClient,
        IAltinnRegisterService altinnRegisterService,
        IBackgroundJobClient backgroundJobClient,
        IPartyRepository partyRepository,
        IOptions<AltinnOptions> altinnOptions,
        ILogger<AltinnEventBus> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(altinnOptions.Value.PlatformGatewayUrl);
        _altinnRegisterService = altinnRegisterService;
        _backgroundJobClient = backgroundJobClient;
        _partyRepository = partyRepository;
        _logger = logger;
        _altinnOptions = altinnOptions.Value;
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, string? subjectOrganizationNumber = null, Guid? guid = null, AltinnEventSubjectRole? subjectRole = null, CancellationToken cancellationToken = default)
    {
        await Publish(type, resourceId, fileTransferId, guid ?? Guid.NewGuid(), DateTime.UtcNow, subjectOrganizationNumber, subjectRole, cancellationToken);
    }

    public async Task Publish(AltinnEventType type, string resourceId, string fileTransferId, Guid eventId, DateTime time, string? subjectOrganizationNumber = null, AltinnEventSubjectRole? subjectRole = null, CancellationToken cancellationToken = default)
    {
        string? partyId = null;
        if (subjectOrganizationNumber != null)
        {
            var party = await _partyRepository.GetParty(subjectOrganizationNumber, cancellationToken);
            if (party == null)
            {
                partyId = await _altinnRegisterService.LookUpOrganizationId(subjectOrganizationNumber, cancellationToken);
                if (partyId != null) await _partyRepository.InitializeParty(subjectOrganizationNumber, partyId);
            }
        }
        var cloudEvent = CreateCloudEvent(type, resourceId, fileTransferId, partyId, subjectOrganizationNumber, subjectRole, eventId, time);
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

    private CloudEvent CreateCloudEvent(AltinnEventType type, string resourceId, string fileTransferId, string? partyId, string? organizationNumber, AltinnEventSubjectRole? subjectRole, Guid? eventId, DateTime time)
    {
        if (organizationNumber is not null && organizationNumber.Contains(":"))
        {
            organizationNumber = organizationNumber.WithoutPrefix();
        }
        Dictionary<string, object>? data = subjectRole.HasValue
            ? new Dictionary<string, object>
            {
                ["Role"] = subjectRole.Value.ToString()
            }
            : null;

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
            Data = data,
        };

        return cloudEvent;
    }
}

