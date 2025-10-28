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
        if (altinnResourceResponse.HasCompetentAuthority.Orgcode.ToLowerInvariant() == "ttd")
        {
            return new ResourceEntity()
            {
                Id = altinnResourceResponse.Identifier,
                ServiceOwnerId = TTD_ORGNUMBER.WithPrefix(),
                OrganizationNumber = TTD_ORGNUMBER,
            };
        }
        return new ResourceEntity()
        {
            Id = altinnResourceResponse.Identifier,
            ServiceOwnerId = altinnResourceResponse.HasCompetentAuthority.Organization.WithPrefix(),
            OrganizationNumber = altinnResourceResponse.HasCompetentAuthority.Organization,
        };
    }
}
