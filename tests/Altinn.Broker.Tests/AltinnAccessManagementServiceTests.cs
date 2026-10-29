using System.Net;
using System.Net.Http.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Altinn.AccessManagement;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Broker.Tests;

public class AltinnAccessManagementServiceTests
{
    [Fact]
    public async Task GetAuthorizedParties_OrganizationWithSubunit_MapsHierarchy()
    {
        using var httpClient = CreateHttpClient(_ => Page(new[]
        {
            new
            {
                partyUuid = "11111111-1111-1111-1111-111111111111",
                name = "Brønnøy sykehus",
                organizationNumber = "922194912",
                partyId = 50001234,
                type = "Organization",
                unitType = "ORGL",
                isDeleted = false,
                onlyHierarchyElementWithNoAccess = true,
                subunits = new[]
                {
                    new
                    {
                        partyUuid = "22222222-2222-2222-2222-222222222222",
                        name = "Brønnøy sykehus avd. Sandnessjøen",
                        organizationNumber = "985616167",
                        partyId = 50001235,
                        type = "Organization",
                        unitType = "BEDR",
                        isDeleted = false,
                        onlyHierarchyElementWithNoAccess = false
                    }
                }
            }
        }));
        var service = CreateService(httpClient);

        var parties = await service.GetAuthorizedParties();

        var party = Assert.Single(parties);
        Assert.Equal("Brønnøy sykehus", party.Name);
        Assert.Equal("922194912", party.OrganizationNumber);
        Assert.Equal(50001234, party.PartyId);
        Assert.Equal("Organization", party.Type);
        Assert.True(party.OnlyHierarchyElementWithNoAccess);

        var subunit = Assert.Single(party.Subunits);
        Assert.Equal("985616167", subunit.OrganizationNumber);
        Assert.False(subunit.OnlyHierarchyElementWithNoAccess);
    }

    [Fact]
    public async Task GetAuthorizedParties_MultiplePages_FollowsNextLink()
    {
        var requestedPaths = new List<string?>();
        using var httpClient = CreateHttpClient(request =>
        {
            requestedPaths.Add(request.RequestUri?.PathAndQuery);
            return requestedPaths.Count == 1
                ? Page([Organization("11111111-1111-1111-1111-111111111111", "922194912")], next: "https://platform.example/accessmanagement/api/v1/enduser/authorizedparties?token=next-page")
                : Page([Organization("22222222-2222-2222-2222-222222222222", "985616167")]);
        });
        var service = CreateService(httpClient);

        var parties = await service.GetAuthorizedParties();

        Assert.Equal(2, parties.Count);
        Assert.Equal(
            [
                "/accessmanagement/api/v1/enduser/authorizedparties?includeSubParties=true",
                "/accessmanagement/api/v1/enduser/authorizedparties?token=next-page"
            ],
            requestedPaths);
    }

    [Fact]
    public async Task GetAuthorizedParties_AuthenticatedEndUser_CallsWithTheirAltinnToken()
    {
        string? authorization = null;
        using var httpClient = CreateHttpClient(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return Page([]);
        });
        var service = CreateService(httpClient);

        await service.GetAuthorizedParties();

        Assert.Equal("Bearer end-user-altinn-token", authorization);
    }

    [Fact]
    public async Task GetAuthorizedParties_NoEndUserSession_Throws()
    {
        using var httpClient = CreateHttpClient(_ => Page([]));
        var service = CreateService(httpClient, altinnToken: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAuthorizedParties());
    }

    [Fact]
    public async Task GetAuthorizedParties_UpstreamFailure_Throws()
    {
        using var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetAuthorizedParties());
    }

    private static object Organization(string partyUuid, string organizationNumber) => new
    {
        partyUuid,
        name = $"Organization {organizationNumber}",
        organizationNumber,
        partyId = 50001234,
        type = "Organization",
        unitType = "ORGL",
        isDeleted = false,
        onlyHierarchyElementWithNoAccess = false
    };

    private static HttpResponseMessage Page(IEnumerable<object> parties, string? next = null) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { data = parties, links = new { next } })
        };

    private static AltinnAccessManagementService CreateService(HttpClient httpClient, string? altinnToken = "end-user-altinn-token")
    {
        var options = Options.Create(new AltinnOptions
        {
            PlatformGatewayUrl = "https://platform.example/"
        });

        return new AltinnAccessManagementService(
            httpClient,
            options,
            new StubEndUserTokenProvider(altinnToken),
            NullLogger<AltinnAccessManagementService>.Instance);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://platform.example/")
        };

    private sealed class StubEndUserTokenProvider(string? altinnToken) : IEndUserTokenProvider
    {
        public Task<string?> GetAltinnToken() => Task.FromResult(altinnToken);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
