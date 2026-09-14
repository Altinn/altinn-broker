using System.Net.Http.Headers;
using System.Net.Http.Json;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.AccessManagement;

/// <summary>
/// Looks up which parties the logged in end user can act on behalf of.
/// Unlike the other platform integrations this one calls with the end user's own Altinn token,
/// not a Maskinporten token, since the party list is personal to the user.
/// </summary>
public class AltinnAccessManagementService : IAltinnAccessManagementService
{
    // Subunits are needed to show the organization hierarchy in the account selector.
    private const string AuthorizedPartiesPath = "accessmanagement/api/v1/enduser/authorizedparties?includeSubParties=true";

    /// <summary>Stops a broken pagination chain from looping forever.</summary>
    private const int MaxPages = 20;

    private readonly HttpClient _httpClient;
    private readonly IEndUserTokenProvider _endUserTokenProvider;
    private readonly ILogger<AltinnAccessManagementService> _logger;

    public AltinnAccessManagementService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IEndUserTokenProvider endUserTokenProvider, ILogger<AltinnAccessManagementService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _endUserTokenProvider = endUserTokenProvider;
        _logger = logger;
    }

    public async Task<List<AuthorizedParty>> GetAuthorizedParties(CancellationToken cancellationToken = default)
    {
        var altinnToken = await _endUserTokenProvider.GetAltinnToken();
        if (string.IsNullOrWhiteSpace(altinnToken))
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated end user request");
        }

        var parties = new List<AuthorizedParty>();
        var url = AuthorizedPartiesPath;
        for (var pageNumber = 0; pageNumber < MaxPages && url is not null; pageNumber++)
        {
            var page = await GetPage(url, altinnToken, cancellationToken);
            parties.AddRange(page.Data
                .Where(party => !string.IsNullOrWhiteSpace(party.PartyUuid))
                .Select(MapParty));
            url = page.Links?.Next;
        }

        if (url is not null)
        {
            _logger.LogWarning("Stopped paging authorized parties after {maxPages} pages. The party list may be incomplete.", MaxPages);
        }

        return parties;
    }

    private async Task<AuthorizedPartiesResponse> GetPage(string url, string altinnToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", altinnToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when getting authorized parties from Altinn Access Management. Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            throw new HttpRequestException($"Altinn Access Management responded {(int)response.StatusCode} when asked for authorized parties");
        }

        var page = await response.Content.ReadFromJsonAsync<AuthorizedPartiesResponse>(cancellationToken);
        if (page is null)
        {
            _logger.LogError("Unexpected json response when getting authorized parties from Altinn Access Management");
            throw new HttpRequestException("Altinn Access Management returned an unexpected response for authorized parties");
        }

        return page;
    }

    private static AuthorizedParty MapParty(AuthorizedPartyDto party) => new()
    {
        PartyUuid = party.PartyUuid!,
        Name = party.Name,
        OrganizationNumber = party.OrganizationNumber,
        PartyId = party.PartyId,
        Type = party.Type ?? "None",
        UnitType = party.UnitType,
        IsDeleted = party.IsDeleted,
        OnlyHierarchyElementWithNoAccess = party.OnlyHierarchyElementWithNoAccess,
        Subunits = party.Subunits?
            .Where(subunit => !string.IsNullOrWhiteSpace(subunit.PartyUuid))
            .Select(MapParty)
            .ToList() ?? []
    };
}
