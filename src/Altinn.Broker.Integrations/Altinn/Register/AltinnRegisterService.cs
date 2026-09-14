using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Altinn.Broker.Application;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Platform.Register.Models;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Register;
public class AltinnRegisterService : IAltinnRegisterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnRegisterService> _logger;
    private readonly HybridCache _cache;

    private static readonly HybridCacheEntryOptions PartyCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24)
    };

    public AltinnRegisterService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnRegisterService> logger, HybridCache cache)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
    }

    public async Task<Party?> LookupPartyByOrganizationNumber(string organizationNumber, CancellationToken cancellationToken = default)
    {
        var organizationWithPrefixFormat = new Regex(@"^\d{4}:\d{9}$");
        if (organizationWithPrefixFormat.IsMatch(organizationNumber))
        {
            organizationNumber = organizationNumber.Substring(5);
        }

        var cacheKey = $"register-party-organization-number:{organizationNumber}";
        try
        {
            var cachedParty = await _cache.GetOptionalAsync<Party>(cacheKey, PartyCacheOptions, cancellationToken);
            if (cachedParty is not null)
            {
                return cachedParty;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Error retrieving organization from cache when looking up organization in Altinn Register");
        }

        var partyLookup = new PartyLookup()
        {
            OrgNo = organizationNumber
        };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/parties/lookup", partyLookup, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up organization in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>(cancellationToken);
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up organization in Altinn Register");
            return null;
        }

        try
        {
            await _cache.SetAsync(cacheKey, party, PartyCacheOptions, cancellationToken: cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Error storing organization in cache when looking up organization in Altinn Register");
        }

        return party;
    }

    public async Task<Party?> LookupPartyByUuid(string partyUuid, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"register-party-uuid:{partyUuid}";
        try
        {
            var cachedParty = await _cache.GetOptionalAsync<Party>(cacheKey, PartyCacheOptions, cancellationToken);
            if (cachedParty is not null)
            {
                return cachedParty;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Error retrieving party from cache when looking up party by uuid in Altinn Register");
        }

        var response = await _httpClient.GetAsync($"register/api/v1/parties/byuuid/{partyUuid}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up party by uuid in Altinn Register. Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>(cancellationToken);
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up party by uuid in Altinn Register");
            return null;
        }

        try
        {
            await _cache.SetAsync(cacheKey, party, PartyCacheOptions, cancellationToken: cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Error storing party in cache when looking up party by uuid in Altinn Register");
        }

        return party;
    }
}
