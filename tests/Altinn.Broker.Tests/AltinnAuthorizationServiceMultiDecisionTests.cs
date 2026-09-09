using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Altinn.Authorization.ABAC.Constants;
using Altinn.Broker.Common.Constants;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Altinn.Authorization;
using Altinn.Common.PEP.Constants;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class AltinnAuthorizationServiceMultiDecisionTests
{
    private const string IdportenIssuer = "https://test.idporten.no";
    private const string Party = "991825827";
    private const string PersonIdentifier = "11887766554";
    private const string WriteAction = "write";
    private const string ReadAction = "read";

    [Fact]
    public async Task GetAuthorizedResources_AsksForEveryResourceAndActionInOneRequest()
    {
        var pdp = new FakePdp(permit: (_, _) => true);
        var service = CreateService(pdp);

        await service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a", "resource-b", "resource-c"]);

        var request = Assert.Single(pdp.Requests);
        using var document = JsonDocument.Parse(request);
        var requestElement = document.RootElement.GetProperty("request");

        var subject = Assert.Single(requestElement.GetProperty("accessSubject").EnumerateArray().ToList());
        Assert.Equal("s1", subject.GetProperty("id").GetString());
        var subjectAttribute = Assert.Single(subject.GetProperty("attribute").EnumerateArray().ToList());
        Assert.Equal(UrnConstants.PersonIdAttribute, subjectAttribute.GetProperty("attributeId").GetString());
        Assert.Equal(PersonIdentifier, subjectAttribute.GetProperty("value").GetString());

        var actions = requestElement.GetProperty("action").EnumerateArray().ToList();
        Assert.Equal(["a1", "a2"], actions.Select(action => action.GetProperty("id").GetString()));
        Assert.Equal([WriteAction, ReadAction], actions.Select(action => AttributeValue(action, XacmlConstants.MatchAttributeIdentifiers.ActionId)));

        var resources = requestElement.GetProperty("resource").EnumerateArray().ToList();
        Assert.Equal(["r1", "r2", "r3"], resources.Select(resource => resource.GetProperty("id").GetString()));
        Assert.Equal(
            ["resource-a", "resource-b", "resource-c"],
            resources.Select(resource => AttributeValue(resource, AltinnXacmlUrns.ResourceId)));
        Assert.All(resources, resource => Assert.Equal(Party, AttributeValue(resource, UrnConstants.OrganizationNumberAttribute)));

        var references = requestElement.GetProperty("multiRequests").GetProperty("requestReference").EnumerateArray().ToList();
        Assert.Equal(6, references.Count);
        Assert.All(references, reference => Assert.Contains("s1", reference.GetProperty("referenceId").EnumerateArray().Select(id => id.GetString())));
    }

    [Fact]
    public async Task GetAuthorizedResources_ReturnsAccessPerResourceAndAction()
    {
        var pdp = new FakePdp(permit: (resourceId, action) =>
            (resourceId == "resource-a" && action == WriteAction)
            || (resourceId == "resource-b" && action == ReadAction)
            || resourceId == "resource-c");
        var service = CreateService(pdp);

        var authorized = await service.GetAuthorizedResources(
            CreateIdportenUser(),
            Party,
            ["resource-a", "resource-b", "resource-c", "resource-d"]);

        Assert.Equal(4, authorized.Count);
        var sender = authorized.Single(resource => resource.ResourceId == "resource-a");
        Assert.True(sender.CanSend);
        Assert.False(sender.CanReceive);

        var recipient = authorized.Single(resource => resource.ResourceId == "resource-b");
        Assert.False(recipient.CanSend);
        Assert.True(recipient.CanReceive);

        var both = authorized.Single(resource => resource.ResourceId == "resource-c");
        Assert.True(both.CanSend);
        Assert.True(both.CanReceive);

        var none = authorized.Single(resource => resource.ResourceId == "resource-d");
        Assert.False(none.CanSend);
        Assert.False(none.CanReceive);
    }

    [Fact]
    public async Task GetAuthorizedResources_WhenDecisionsAreOutOfOrder_MapsThemFromTheEchoedAttributes()
    {
        var pdp = new FakePdp(
            permit: (resourceId, action) => resourceId == "resource-a" && action == WriteAction,
            reverseDecisions: true);
        var service = CreateService(pdp);

        var authorized = await service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a", "resource-b"]);

        Assert.True(authorized.Single(resource => resource.ResourceId == "resource-a").CanSend);
        Assert.False(authorized.Single(resource => resource.ResourceId == "resource-b").CanSend);
        Assert.False(authorized.Single(resource => resource.ResourceId == "resource-b").CanReceive);
    }

    [Fact]
    public async Task GetAuthorizedResources_WhenAttributesAreNotEchoed_MapsDecisionsByRequestOrder()
    {
        var pdp = new FakePdp(
            permit: (resourceId, action) => resourceId == "resource-b" && action == ReadAction,
            echoCategories: false);
        var service = CreateService(pdp);

        var authorized = await service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a", "resource-b"]);

        Assert.False(authorized.Single(resource => resource.ResourceId == "resource-a").CanReceive);
        Assert.True(authorized.Single(resource => resource.ResourceId == "resource-b").CanReceive);
    }

    [Fact]
    public async Task GetAuthorizedResources_SplitsLargeResourceSetsAcrossSeveralRequests()
    {
        var pdp = new FakePdp(permit: (_, _) => true);
        var service = CreateService(pdp);
        var resourceIds = Enumerable.Range(1, 201).Select(number => $"resource-{number}").ToList();

        var authorized = await service.GetAuthorizedResources(CreateIdportenUser(), Party, resourceIds);

        Assert.Equal(201, authorized.Count);
        Assert.All(authorized, resource => Assert.True(resource.CanSend && resource.CanReceive));
        Assert.Equal(2, pdp.Requests.Count);
        Assert.Equal([200, 1], pdp.Requests.Select(CountResources));
    }

    [Fact]
    public async Task GetAuthorizedResources_WhenAuthorizationReturnsAnError_Throws()
    {
        var pdp = new FakePdp(permit: (_, _) => true, statusCode: HttpStatusCode.BadGateway);
        var service = CreateService(pdp);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a"]));
    }

    [Fact]
    public async Task GetAuthorizedResources_WhenAuthorizationReturnsWrongNumberOfDecisions_Throws()
    {
        var pdp = new FakePdp(permit: (_, _) => true, responseOverride: """{ "Response": [{ "Decision": "Permit" }] }""");
        var service = CreateService(pdp);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a"]));
    }

    [Theory]
    [InlineData("idporten-loa-high", true)]
    [InlineData("idporten-loa-substantial", false)]
    public async Task GetAuthorizedResources_HonoursTheMinimumAuthenticationLevelObligation(string authenticationContext, bool expectedAccess)
    {
        var pdp = new FakePdp(permit: (_, _) => true, minimumAuthenticationLevel: 4);
        var service = CreateService(pdp);
        var user = CreateIdportenUser(new Claim(IdportenXacmlMapper.AuthenticationContextClaim, authenticationContext, ClaimValueTypes.String, IdportenIssuer));

        var authorized = await service.GetAuthorizedResources(user, Party, ["resource-a"]);

        Assert.Equal(expectedAccess, authorized.Single().CanSend);
    }

    [Fact]
    public async Task GetAuthorizedResources_WithIdportenTokenWithoutPid_ReturnsNoAccessWithoutCallingPdp()
    {
        var pdp = new FakePdp(permit: (_, _) => true);
        var service = CreateService(pdp);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", IdportenIssuer, ClaimValueTypes.String, IdportenIssuer)],
            "Test"));

        var authorized = await service.GetAuthorizedResources(user, Party, ["resource-a"]);

        Assert.Empty(authorized);
        Assert.Empty(pdp.Requests);
    }

    [Fact]
    public async Task GetAuthorizedResources_WithoutResources_DoesNotCallPdp()
    {
        var pdp = new FakePdp(permit: (_, _) => true);
        var service = CreateService(pdp);

        var authorized = await service.GetAuthorizedResources(CreateIdportenUser(), Party, []);

        Assert.Empty(authorized);
        Assert.Empty(pdp.Requests);
    }

    [Fact]
    public async Task GetAuthorizedResources_WhenOneResourceCannotBeResolved_StillReturnsTheOthers()
    {
        var pdp = new FakePdp(
            permit: (_, _) => true,
            failFor: resources => resources.Contains("resource-broken"));
        var service = CreateService(pdp);

        var authorized = await service.GetAuthorizedResources(
            CreateIdportenUser(),
            Party,
            ["resource-a", "resource-broken", "resource-b"]);

        Assert.True(authorized.Single(resource => resource.ResourceId == "resource-a").CanSend);
        Assert.True(authorized.Single(resource => resource.ResourceId == "resource-b").CanSend);
        var broken = authorized.Single(resource => resource.ResourceId == "resource-broken");
        Assert.False(broken.CanSend);
        Assert.False(broken.CanReceive);
        // The failed batch, then one request per resource in it.
        Assert.Equal(4, pdp.Requests.Count);
    }

    [Fact]
    public async Task GetAuthorizedResources_WhenAuthorizationFailsForEveryResource_Throws()
    {
        var pdp = new FakePdp(permit: (_, _) => true, failFor: _ => true);
        var service = CreateService(pdp);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a", "resource-b"]));
    }

    [Fact]
    public async Task GetAuthorizedResources_WithASingleResourceThatFails_DoesNotRetry()
    {
        var pdp = new FakePdp(permit: (_, _) => true, failFor: _ => true);
        var service = CreateService(pdp);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetAuthorizedResources(CreateIdportenUser(), Party, ["resource-a"]));

        Assert.Single(pdp.Requests);
    }

    private static int CountResources(string requestJson) => RequestedResources(requestJson).Count;

    private static List<string> RequestedResources(string requestJson)
    {
        using var document = JsonDocument.Parse(requestJson);
        return document.RootElement
            .GetProperty("request")
            .GetProperty("resource")
            .EnumerateArray()
            .Select(resource => AttributeValue(resource, AltinnXacmlUrns.ResourceId)!)
            .ToList();
    }

    private static string? AttributeValue(JsonElement category, string attributeId)
        => category.GetProperty("attribute")
            .EnumerateArray()
            .FirstOrDefault(attribute => attribute.GetProperty("attributeId").GetString() == attributeId)
            .GetProperty("value")
            .GetString();

    private static AltinnAuthorizationService CreateService(FakePdp pdp)
    {
        var httpClient = new HttpClient(pdp)
        {
            BaseAddress = new Uri("https://unit.test/")
        };

        return new AltinnAuthorizationService(
            httpClient,
            Options.Create(new AltinnOptions { PlatformSubscriptionKey = "test-subscription-key" }),
            Mock.Of<IResourceRepository>(),
            Mock.Of<ILogger<AltinnAuthorizationService>>());
    }

    private static ClaimsPrincipal CreateIdportenUser(params Claim[] additionalClaims)
    {
        var claims = new List<Claim>
        {
            new("iss", IdportenIssuer, ClaimValueTypes.String, IdportenIssuer),
            new("pid", PersonIdentifier, ClaimValueTypes.String, IdportenIssuer)
        };
        if (additionalClaims.All(claim => claim.Type != IdportenXacmlMapper.AuthenticationContextClaim))
        {
            claims.Add(new Claim(IdportenXacmlMapper.AuthenticationContextClaim, "idporten-loa-high", ClaimValueTypes.String, IdportenIssuer));
        }
        claims.AddRange(additionalClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // Answers one decision per requested (resource, action) pair, the way Authorization does.
    private sealed class FakePdp(
        Func<string, string, bool> permit,
        bool echoCategories = true,
        bool reverseDecisions = false,
        int? minimumAuthenticationLevel = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? responseOverride = null,
        Func<List<string>, bool>? failFor = null) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestJson = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(requestJson);

            if (failFor?.Invoke(RequestedResources(requestJson)) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(IndeterminateResponse, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseOverride ?? CreateResponse(requestJson), Encoding.UTF8, "application/json")
            };
        }

        // What real Authorization answers when it cannot resolve one of the resources.
        private const string IndeterminateResponse =
            """{ "response": [{ "decision": "Indeterminate" }] }""";

        private string CreateResponse(string requestJson)
        {
            using var document = JsonDocument.Parse(requestJson);
            var requestElement = document.RootElement.GetProperty("request");
            var resourcesById = requestElement.GetProperty("resource")
                .EnumerateArray()
                .ToDictionary(
                    resource => resource.GetProperty("id").GetString()!,
                    resource => AttributeValue(resource, AltinnXacmlUrns.ResourceId)!);
            var actionsById = requestElement.GetProperty("action")
                .EnumerateArray()
                .ToDictionary(
                    action => action.GetProperty("id").GetString()!,
                    action => AttributeValue(action, XacmlConstants.MatchAttributeIdentifiers.ActionId)!);

            var decisions = new List<string>();
            foreach (var reference in requestElement.GetProperty("multiRequests").GetProperty("requestReference").EnumerateArray())
            {
                var referenceIds = reference.GetProperty("referenceId").EnumerateArray().Select(id => id.GetString()!).ToList();
                var resourceId = resourcesById[referenceIds.Single(resourcesById.ContainsKey)];
                var action = actionsById[referenceIds.Single(actionsById.ContainsKey)];
                decisions.Add(CreateDecision(resourceId, action));
            }
            if (reverseDecisions)
            {
                decisions.Reverse();
            }

            return $$"""{ "Response": [{{string.Join(",", decisions)}}] }""";
        }

        private string CreateDecision(string resourceId, string action)
        {
            var decision = permit(resourceId, action) ? "Permit" : "NotApplicable";
            var categories = echoCategories
                ? $$"""
                    ,"Category": [
                      { "Attribute": [{ "AttributeId": "{{AltinnXacmlUrns.ResourceId}}", "Value": "{{resourceId}}" }] },
                      { "Attribute": [{ "AttributeId": "{{XacmlConstants.MatchAttributeIdentifiers.ActionId}}", "Value": "{{action}}" }] }
                    ]
                    """
                : string.Empty;
            var obligations = minimumAuthenticationLevel is null
                ? string.Empty
                : $$"""
                    ,"Obligations": [{
                      "Id": "authentication-level",
                      "AttributeAssignment": [{
                        "AttributeId": "minimum-authentication-level",
                        "Value": "{{minimumAuthenticationLevel}}",
                        "Category": "{{UrnConstants.MinimumAuthenticationLevel}}",
                        "DataType": "string",
                        "Issuer": "Altinn"
                      }]
                    }]
                    """;

            return $$"""{ "Decision": "{{decision}}"{{categories}}{{obligations}} }""";
        }
    }
}
