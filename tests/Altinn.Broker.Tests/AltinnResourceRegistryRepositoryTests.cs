using System.Net;
using System.Net.Http.Json;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Integrations.Altinn.ResourceRegistry;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Broker.Tests;

public class AltinnResourceRegistryRepositoryTests
{
    [Fact]
    public async Task GetAccessListOfResource_ValidPartyWithoutMembership_ReturnsEmptyList()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { data = Array.Empty<object>() })
            }));
        var repository = CreateRepository(httpClient);

        var result = await repository.GetAccessListOfResource("test-resource", "986252932");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAccessListOfResource_InvalidParty_ReturnsNull()
    {
        Uri? requestUri = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }));
        var repository = CreateRepository(httpClient);

        var result = await repository.GetAccessListOfResource("test-resource", "111111111");

        Assert.Null(result);
        Assert.Equal(
            "/resourceregistry/api/v1/access-lists/memberships?resource=urn:altinn:resource:test-resource&party=urn:altinn:organization:identifier-no:111111111",
            requestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetAccessListOfResource_UpstreamFailure_Throws()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var repository = CreateRepository(httpClient);

        await Assert.ThrowsAsync<BadHttpRequestException>(() =>
            repository.GetAccessListOfResource("test-resource", "991825827"));
    }

    private static AltinnResourceRegistryRepository CreateRepository(HttpClient httpClient)
    {
        var options = Options.Create(new AltinnOptions
        {
            PlatformGatewayUrl = "https://platform.example/"
        });

        return new AltinnResourceRegistryRepository(
            httpClient,
            options,
            NullLogger<AltinnResourceRegistryRepository>.Instance);
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
