using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Platform.Register.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Altinn.Register;
public class AltinnRegisterService : IAltinnRegisterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnRegisterService> _logger;

    public AltinnRegisterService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnRegisterService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> LookUpOrganizationId(string organizationId, CancellationToken cancellationToken = default)
    {
        var organizationWithPrefixFormat = new Regex(@"^\d{4}:\d{9}$");
        if (organizationWithPrefixFormat.IsMatch(organizationId))
        {
            organizationId = organizationId.Substring(5);
        }
        var partyLookup = new PartyLookup()
        {
            OrgNo = organizationId
        };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/parties/lookup", partyLookup, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up organization in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>();
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up organization in Altinn Register");
            return null;
        }
        return party.PartyId.ToString();
    }
}
