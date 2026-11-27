using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Altinn.Organizations;

/// <summary>
/// Reads organization numbers from the Altinn organization list.
/// The file changes rarely, so a successful read is kept for 24 hours.
/// </summary>
public class AltinnOrganizationCatalog(
    HttpClient httpClient,
    HybridCache cache,
    ILogger<AltinnOrganizationCatalog> logger) : IAltinnOrganizationCatalog
{
    public const string CatalogUrl = "https://raw.githubusercontent.com/Altinn/altinn-cdn/refs/heads/master/orgs/altinn-orgs.json";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24)
    };

    public async Task<bool> IsPossibleServiceOwner(string organizationNumber, CancellationToken cancellationToken)
    {
        try
        {
            var organizationNumbers = await cache.GetOrCreateAsync(
                "altinn-orgs",
                this,
                (catalog, token) => new ValueTask<string[]>(catalog.LoadOrganizationNumbers(token)),
                CacheOptions,
                cancellationToken: cancellationToken);
            return organizationNumbers.Contains(organizationNumber.WithoutPrefix(), StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not load the Altinn organization catalog");
            return false;
        }
    }

    private async Task<string[]> LoadOrganizationNumbers(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(CatalogUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<AltinnOrganizationFile>(cancellationToken)
            ?? throw new JsonException("Altinn organization catalog was empty.");
        return document.Orgs.Values
            .Select(organization => organization.OrganizationNumber)
            .Where(organizationNumber => !string.IsNullOrWhiteSpace(organizationNumber))
            .Select(organizationNumber => organizationNumber!.WithoutPrefix())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class AltinnOrganizationFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("orgs")]
        public Dictionary<string, AltinnOrganization> Orgs { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class AltinnOrganization
    {
        [System.Text.Json.Serialization.JsonPropertyName("orgnr")]
        public string? OrganizationNumber { get; set; }
    }
}
