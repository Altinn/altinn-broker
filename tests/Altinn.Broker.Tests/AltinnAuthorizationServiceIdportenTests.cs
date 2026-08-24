using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Common.Constants;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Altinn.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class AltinnAuthorizationServiceIdportenTests
{
    private const string IdportenIssuer = "https://test.idporten.no";
    private const string ResourceId = "test-resource";
    private const string OrganizationNumber = "991825827";

    [Fact]
    public async Task CheckAccessAsSender_WithIdportenToken_MapsOnlyPidToAccessSubject()
    {
        var handler = new PdpResponseHandler(CreatePermitResponse(minimumAuthenticationLevel: 3));
        var service = CreateService(handler);
        var user = CreateIdportenUser(
            new Claim("pid", "11887766554", ClaimValueTypes.String, IdportenIssuer),
            new Claim("urn:altinn:userid", "12345", ClaimValueTypes.String, IdportenIssuer),
            new Claim(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-substantial", ClaimValueTypes.String, IdportenIssuer));

        var hasAccess = await service.CheckAccessAsSender(user, ResourceId, OrganizationNumber);

        Assert.True(hasAccess);
        Assert.NotNull(handler.LastRequestJson);

        using var request = JsonDocument.Parse(handler.LastRequestJson);
        var subjectAttributes = request.RootElement
            .GetProperty("request")
            .GetProperty("accessSubject")[0]
            .GetProperty("attribute")
            .EnumerateArray()
            .ToList();

        var subjectAttribute = Assert.Single(subjectAttributes);
        Assert.Equal(UrnConstants.PersonIdAttribute, subjectAttribute.GetProperty("attributeId").GetString());
        Assert.Equal("11887766554", subjectAttribute.GetProperty("value").GetString());
        Assert.DoesNotContain("urn:altinn:userid", handler.LastRequestJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAccessAsSender_WithIdportenTokenWithoutPid_DeniesWithoutCallingPdp()
    {
        var handler = new PdpResponseHandler(CreatePermitResponse());
        var service = CreateService(handler);
        var user = CreateIdportenUser(
            new Claim(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-high", ClaimValueTypes.String, IdportenIssuer));

        var hasAccess = await service.CheckAccessAsSender(user, ResourceId, OrganizationNumber);

        Assert.False(hasAccess);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-high", 4, true)]
    [InlineData(IdportenXacmlMapper.MappedAuthenticationContextClaim, "idporten-loa-high", 4, true)]
    [InlineData(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-substantial", 3, true)]
    [InlineData(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-substantial", 4, false)]
    [InlineData(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-low", 2, true)]
    [InlineData(IdportenXacmlMapper.AuthenticationContextClaim, "selfregistered-email", 1, false)]
    [InlineData(IdportenXacmlMapper.AuthenticationContextClaim, "unknown", 0, false)]
    public async Task CheckAccessAsSender_WithAuthenticationLevelObligation_UsesIdportenAcr(
        string claimType,
        string authenticationContext,
        int minimumAuthenticationLevel,
        bool expectedAccess)
    {
        var handler = new PdpResponseHandler(CreatePermitResponse(minimumAuthenticationLevel));
        var service = CreateService(handler);
        var user = CreateIdportenUser(
            new Claim("pid", "11887766554", ClaimValueTypes.String, IdportenIssuer),
            new Claim(claimType, authenticationContext, ClaimValueTypes.String, IdportenIssuer));

        var hasAccess = await service.CheckAccessAsSender(user, ResourceId, OrganizationNumber);

        Assert.Equal(expectedAccess, hasAccess);
    }

    [Fact]
    public async Task CheckAccessAsSender_WhenAnyPdpDecisionIsDeny_DeniesAccess()
    {
        const string response = """
            {
              "Response": [
                { "Decision": "Permit" },
                { "Decision": "Deny" }
              ]
            }
            """;
        var handler = new PdpResponseHandler(response);
        var service = CreateService(handler);
        var user = CreateIdportenUser(
            new Claim("pid", "11887766554", ClaimValueTypes.String, IdportenIssuer));

        var hasAccess = await service.CheckAccessAsSender(user, ResourceId, OrganizationNumber);

        Assert.False(hasAccess);
    }

    [Theory]
    [InlineData("https://idporten.no", true)]
    [InlineData("https://test.idporten.no/", true)]
    [InlineData("https://test.idporten.no.evil.example", false)]
    [InlineData("http://test.idporten.no", false)]
    [InlineData("https://platform.tt02.altinn.no/authentication/api/v1/openid/", false)]
    public void IsIdportenToken_ValidatesTheIssuerHost(string issuer, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", issuer)], "Test"));

        Assert.Equal(expected, IdportenXacmlMapper.IsIdportenToken(user));
    }

    private static AltinnAuthorizationService CreateService(PdpResponseHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://unit.test/")
        };
        var resourceRepository = new Mock<IResourceRepository>(MockBehavior.Strict);
        resourceRepository
            .Setup(repository => repository.GetResource(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceEntity
            {
                Id = ResourceId,
                ServiceOwnerId = $"urn:altinn:organization:identifier-no:{OrganizationNumber}"
            });

        return new AltinnAuthorizationService(
            httpClient,
            Options.Create(new AltinnOptions { PlatformSubscriptionKey = "test-subscription-key" }),
            resourceRepository.Object,
            Mock.Of<ILogger<AltinnAuthorizationService>>());
    }

    private static ClaimsPrincipal CreateIdportenUser(params Claim[] additionalClaims)
    {
        var claims = new List<Claim>
        {
            new("iss", IdportenIssuer, ClaimValueTypes.String, IdportenIssuer)
        };
        claims.AddRange(additionalClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static string CreatePermitResponse(int? minimumAuthenticationLevel = null)
    {
        if (minimumAuthenticationLevel is null)
        {
            return """{ "Response": [{ "Decision": "Permit" }] }""";
        }

        return $$"""
            {
              "Response": [{
                "Decision": "Permit",
                "Obligations": [{
                  "Id": "authentication-level",
                  "AttributeAssignment": [{
                    "AttributeId": "minimum-authentication-level",
                    "Value": "{{minimumAuthenticationLevel}}",
                    "Category": "{{UrnConstants.MinimumAuthenticationLevel}}",
                    "DataType": "string",
                    "Issuer": "Altinn"
                  }]
                }]
              }]
            }
            """;
    }

    private sealed class PdpResponseHandler(string responseJson) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? LastRequestJson { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestJson = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
