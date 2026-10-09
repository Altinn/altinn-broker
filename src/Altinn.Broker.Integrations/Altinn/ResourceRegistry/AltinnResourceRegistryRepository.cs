using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;
public class AltinnResourceRegistryRepository : IAltinnResourceRepository
{
    private readonly HttpClient _client;
    private readonly ILogger<AltinnResourceRegistryRepository> _logger;

    private const string TTD_ORGNUMBER = "991825827";

    private const int MaxAccessListPages = 50; //access lists by owner has a page size of 20 and members and resource connections have a page size of 100.

    /// <summary>Matches what ReadFromJsonAsync uses, so casing is handled the same way.</summary>
    private static readonly JsonSerializerOptions PaginatedJsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<List<string>?> GetAccessListOfResource(string resourceId, string party, CancellationToken cancellationToken = default)
    {
        var url = $"resourceregistry/api/v1/access-lists/memberships?resource=urn:altinn:resource:{resourceId}&party=urn:altinn:organization:identifier-no:{party}";
        var response = await _client.GetAsync(url, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.NoContent => null,
            HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<AccessListMembershipResponse>(cancellationToken: cancellationToken))
                ?.Data
                ?.Select(m => m.Party?.Split(":").Last())
                .OfType<string>()
                .ToList(),
            _ => throw new BadHttpRequestException("Failed to get access list from Altinn Resource Registry")
        };
    }

    public async Task<ResourceAccessList?> GetAccessListMembersOfResource(string resourceId, CancellationToken cancellationToken = default)
    {
        var resource = await GetResourceFromRegistry(resourceId, cancellationToken);
        if (resource is null)
        {
            return null;
        }
        if (resource.AccessListMode is not "Enabled")
        {
            _logger.LogInformation(
                "Resource {resourceId} has access list mode {accessListMode}, so it does not restrict who may receive",
                resourceId.SanitizeForLogs(),
                resource.AccessListMode);
            return new ResourceAccessList { Restricted = false, PartyUuids = [] };
        }

        var owner = Uri.EscapeDataString(resource.HasCompetentAuthority.Orgcode);
        var partyUuids = new List<string>();
        foreach (var identifier in await GetConnectedAccessLists(owner, resourceId, cancellationToken))
        {
            var accessListMembers = await GetAllPages<AccessListMemberResponse>(
                $"resourceregistry/api/v1/access-lists/{owner}/{Uri.EscapeDataString(identifier)}/members",
                cancellationToken);
            partyUuids.AddRange(accessListMembers.Select(member => member.Id?.WithoutPrefix()).OfType<string>());
        }

        return new ResourceAccessList { Restricted = true, PartyUuids = partyUuids.Distinct().ToList() };
    }

    /// <remarks>
    /// Resource Registry returns every list the owner has and only filters the connections it loads,
    /// so lists that came back without a connection to this resource are dropped here.
    /// </remarks>
    private async Task<List<string>> GetConnectedAccessLists(string owner, string resourceId, CancellationToken cancellationToken)
    {
        var GetAllAccessListsConnectedToResourceUrl = $"resourceregistry/api/v1/access-lists/{owner}?resource={Uri.EscapeDataString(resourceId)}&include=resources";
        var lists = await GetAllPages<AccessListInfoResponse>(GetAllAccessListsConnectedToResourceUrl, cancellationToken);

        _logger.LogDebug("{owner} owns {listCount} access list(s) to check against resource {resourceId}", owner, lists.Count, resourceId.SanitizeForLogs());
        return lists
            .Where(list => list.ResourceConnections?.Count > 0)
            .Select(list => list.Identifier)
            .OfType<string>()
            .Distinct()
            .ToList();
    }

    private async Task<List<T>> GetAllPages<T>(string url, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        string? token = null;

        for (var page = 0; page < MaxAccessListPages; page++)
        {
            var pageUrl = token is null ? url : QueryHelpers.AddQueryString(url, "token", token);
            var response = await _client.GetAsync(pageUrl, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return items;
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError("Failed to get access lists from Altinn Resource Registry. Status code: {StatusCode}", response.StatusCode);
                throw new BadHttpRequestException("Failed to get access lists from Altinn Resource Registry");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var body = JsonSerializer.Deserialize<PaginatedResponse<T>>(json, PaginatedJsonOptions);
            if (body?.Items is null)
            {
                _logger.LogWarning(
                    "Altinn Resource Registry returned a response without a data array for {url}. Body: {body}",
                    url.SanitizeForLogs(),
                    json.SanitizeForLogs());
                return items;
            }
            items.AddRange(body.Items);

            token = GetContinuationToken(body.Links?.Next);
            if (token is null)
            {
                return items;
            }
        }

        return items;
    }

    private static string? GetContinuationToken(string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink) || !Uri.TryCreate(nextLink, UriKind.Absolute, out var uri))
        {
            return null;
        }
        return QueryHelpers.ParseQuery(uri.Query).TryGetValue("token", out var token) ? token.ToString() : null;
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
