using System.Net;
using System.Net.Http.Json;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;
public class AltinnResourceRegistryRepository : IAltinnResourceRepository
{
    private readonly HttpClient _client;
    private readonly ILogger<AltinnResourceRegistryRepository> _logger;

    private const string TTD_ORGNUMBER = "991825827";

    public AltinnResourceRegistryRepository(HttpClient httpClient, IOptions<AltinnOptions> options, ILogger<AltinnResourceRegistryRepository> logger)
    {
        httpClient.BaseAddress = new Uri(options.Value.PlatformGatewayUrl);
        _client = httpClient;
        _logger = logger;
    }

    public async Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        var altinnResourceResponse = await GetResourceFromRegistry(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            return null;
        }
        if (altinnResourceResponse.HasCompetentAuthority.Orgcode.ToLowerInvariant() == "ttd")
        {
            return new ResourceEntity()
            {
                Id = altinnResourceResponse.Identifier,
                ServiceOwnerId = TTD_ORGNUMBER.WithPrefix(),
                OrganizationNumber = TTD_ORGNUMBER,
                AccessListEnabled = altinnResourceResponse.AccessListMode is "Enabled"
            };
        }
        return new ResourceEntity()
        {
            Id = altinnResourceResponse.Identifier,
            ServiceOwnerId = altinnResourceResponse.HasCompetentAuthority.Organization.WithPrefix(),
            OrganizationNumber = altinnResourceResponse.HasCompetentAuthority.Organization,
            AccessListEnabled = altinnResourceResponse.AccessListMode is "Enabled"
        };
    }

    public async Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken = default)
    {
        var altinnResourceResponse = await GetResourceFromRegistry(resourceId, cancellationToken);

        return altinnResourceResponse is null ? null : GetNameOfResourceResponse(altinnResourceResponse);
    }

    public async Task<AltinnResourceMetadata?> GetResourceMetadata(string resourceId, CancellationToken cancellationToken = default)
    {
        var altinnResourceResponse = await GetResourceFromRegistry(resourceId, cancellationToken);
        if (altinnResourceResponse is null)
        {
            return null;
        }

        return new AltinnResourceMetadata
        {
            Title = PickPreferredLanguage(altinnResourceResponse.Title),
            ServiceOwnerName = GetNameOfResourceResponse(altinnResourceResponse)
        };
    }

    public async Task<List<string>?> GetAccessListOfResource(string resourceId, string party, CancellationToken cancellationToken = default)
    {
        var url = $"resourceregistry/api/v1/access-lists/memberships?resource=urn:altinn:resource:{resourceId}&party=urn:altinn:organization:identifier-no:{party}";
        var response = await _client.GetAsync(url, cancellationToken);
        return response.StatusCode switch
        {
            // Resource Registry returns 400 when the party URN is invalid or cannot be resolved.
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.NoContent => null,
            HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<AccessListMembershipResponse>(cancellationToken: cancellationToken))
                ?.Data
                ?.Select(m => m.Party?.Split(":").Last())
                .OfType<string>()
                .ToList(),
            _ => throw new BadHttpRequestException("Failed to get access list from Altinn Resource Registry")
        };
    }

    

    private async Task<GetResourceResponse?> GetResourceFromRegistry(string resourceId, CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync($"resourceregistry/api/v1/resource/{resourceId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("Failed to get resource from Altinn Resource Registry. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
            throw new BadHttpRequestException("Failed to get resource from Altinn Resource Registry");
        }
        var altinnResourceResponse = await response.Content.ReadFromJsonAsync<GetResourceResponse>(cancellationToken: cancellationToken);
        if (altinnResourceResponse is null)
        {
            _logger.LogError("Failed to deserialize response from Altinn Resource Registry");
            throw new BadHttpRequestException("Failed to process response from Altinn Resource Registry");
        }
        return altinnResourceResponse;
    }

    private string GetNameOfResourceResponse(GetResourceResponse resourceResponse)
        => PickPreferredLanguage(resourceResponse.HasCompetentAuthority.Name) ?? string.Empty;

    private static string? PickPreferredLanguage(Dictionary<string, string>? translations)
    {
        if (translations is null)
        {
            return null;
        }

        foreach (var language in new[] { "nb", "nn", "en" })
        {
            if (translations.TryGetValue(language, out var translation) && !string.IsNullOrWhiteSpace(translation))
            {
                return translation;
            }
        }
        return null;
    }
}
