using System.Security.Claims;

using Altinn.Broker.Application;
using Altinn.Broker.Application.GetAuthorizedResources;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Tus;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class GetAuthorizedResourcesHandlerTests
{
    private const string Party = "991825827";
    private const string OtherOwner = "123456789";

    [Theory]
    [InlineData("")]
    [InlineData("not-an-organization-number")]
    [InlineData("12345678")]
    public async Task Process_WithInvalidParty_ReturnsInvalidPartyError(string party)
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var handler = CreateHandler(cache, new Mock<IResourceRepository>(MockBehavior.Strict), authorizationService, new Mock<IAltinnResourceRepository>());

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.InvalidParty, result.AsT1);
    }

    [Fact]
    public async Task Process_ReturnsOnlyAccessListMembersWithSendOrReceive()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = CreateResourceRepository(
            (Id: "resource-a", Owner: OtherOwner),
            (Id: "resource-b", Owner: OtherOwner),
            (Id: "resource-c", Owner: OtherOwner));
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), Party, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AuthorizedResource { ResourceId = "resource-a", CanSend = true },
                new AuthorizedResource { ResourceId = "resource-b", CanReceive = true },
                new AuthorizedResource { ResourceId = "resource-c" }
            ]);
        var altinnResourceRepository = CreateAltinnResourceRepository(
            ("resource-a", "Røntgenbilder mellom sykehus", "Helsedirektoratet"),
            ("resource-b", "Avviksrapport", "Arbeidstilsynet"));
        SetupAccessListMembership(altinnResourceRepository, "resource-a", Party, member: true);
        SetupAccessListMembership(altinnResourceRepository, "resource-b", Party, member: true);
        SetupAccessListMembership(altinnResourceRepository, "resource-c", Party, member: false);
        var handler = CreateHandler(cache, resourceRepository, authorizationService, altinnResourceRepository);

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = Party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        var resources = result.AsT0;
        Assert.Equal(["resource-b", "resource-a"], resources.Select(resource => resource.ResourceId));

        var sender = resources.Single(resource => resource.ResourceId == "resource-a");
        Assert.True(sender.CanSend);
        Assert.False(sender.CanReceive);
        Assert.False(sender.CanPublish);
        Assert.False(sender.IsOwned);
        Assert.Equal("Røntgenbilder mellom sykehus", sender.Name);
        Assert.Equal("Helsedirektoratet", sender.ServiceOwnerName);
    }

    [Fact]
    public async Task Process_ExcludesResourcesWherePartyIsNotOnAccessList()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = CreateResourceRepository((Id: "resource-a", Owner: OtherOwner));
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), Party, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AuthorizedResource { ResourceId = "resource-a", CanSend = true }]);
        var altinnResourceRepository = CreateAltinnResourceRepository(
            ("resource-a", "Røntgenbilder mellom sykehus", "Helsedirektoratet"));
        SetupAccessListMembership(altinnResourceRepository, "resource-a", Party, member: false);
        var handler = CreateHandler(cache, resourceRepository, authorizationService, altinnResourceRepository);

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = Party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
    }

    [Fact]
    public async Task Process_IncludesOwnedResourcesForServiceOwnerWithoutSendOrReceive()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = CreateResourceRepository((Id: "resource-owned", Owner: Party));
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), Party, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AuthorizedResource { ResourceId = "resource-owned" }]);
        var altinnResourceRepository = CreateAltinnResourceRepository(
            ("resource-owned", "Eid tjeneste", "Digitaliseringsdirektoratet"));
        var serviceOwnerRepository = new Mock<IServiceOwnerRepository>();
        serviceOwnerRepository
            .Setup(repository => repository.GetServiceOwner($"0192:{Party}"))
            .ReturnsAsync(new ServiceOwnerEntity
            {
                Id = $"0192:{Party}",
                Name = "Test owner",
                StorageProviders = []
            });
        var handler = CreateHandler(cache, resourceRepository, authorizationService, altinnResourceRepository, serviceOwnerRepository);

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = Party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        var resource = Assert.Single(result.AsT0);
        Assert.Equal("resource-owned", resource.ResourceId);
        Assert.False(resource.CanSend);
        Assert.False(resource.CanReceive);
        Assert.True(resource.IsServiceOwner);
        Assert.True(resource.IsOwned);
        Assert.False(resource.CanPublish);
    }

    [Fact]
    public async Task Process_ChecksEveryConfiguredResourceWithThePartyWithoutPrefix()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = CreateResourceRepository(
            (Id: "resource-a", Owner: OtherOwner),
            (Id: "resource-b", Owner: OtherOwner));
        var authorizationService = new Mock<IAuthorizationService>();
        IReadOnlyList<string>? checkedResourceIds = null;
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), Party, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimsPrincipal?, string, IReadOnlyList<string>, CancellationToken>((_, _, resourceIds, _) => checkedResourceIds = resourceIds)
            .ReturnsAsync([]);
        var handler = CreateHandler(cache, resourceRepository, authorizationService, new Mock<IAltinnResourceRepository>());

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = $"0192:{Party}" }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
        Assert.Equal(["resource-a", "resource-b"], checkedResourceIds);
    }

    [Fact]
    public async Task Process_WhenResourceRegistryIsUnavailable_ReturnsTheResourceWithoutPresentationMetadata()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = CreateResourceRepository((Id: "resource-a", Owner: OtherOwner));
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), Party, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AuthorizedResource { ResourceId = "resource-a", CanSend = true }]);
        var altinnResourceRepository = new Mock<IAltinnResourceRepository>();
        altinnResourceRepository
            .Setup(repository => repository.GetResourceMetadata(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadHttpRequestException("Resource Registry is down"));
        SetupAccessListMembership(altinnResourceRepository, "resource-a", Party, member: true);
        var handler = CreateHandler(cache, resourceRepository, authorizationService, altinnResourceRepository);

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = Party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        var resource = Assert.Single(result.AsT0);
        Assert.Equal("resource-a", resource.ResourceId);
        Assert.Null(resource.Name);
        Assert.Null(resource.ServiceOwnerName);
        Assert.True(resource.CanSend);
    }

    [Fact]
    public async Task Process_WhenAuthorizationIsUnavailable_ReturnsError()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = CreateResourceRepository((Id: "resource-a", Owner: OtherOwner));
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), Party, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Authorization returned 502 for multi decision request"));
        var handler = CreateHandler(cache, resourceRepository, authorizationService, new Mock<IAltinnResourceRepository>());

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = Party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.AuthorizationUnavailable, result.AsT1);
    }

    [Fact]
    public async Task Process_WithoutConfiguredResources_ReturnsEmptyListWithoutAskingPdp()
    {
        await using var cache = TestHybridCacheFactory.CreateScope();
        var resourceRepository = new Mock<IResourceRepository>();
        resourceRepository
            .Setup(repository => repository.GetResources(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var handler = CreateHandler(cache, resourceRepository, authorizationService, new Mock<IAltinnResourceRepository>());

        var result = await handler.Process(new GetAuthorizedResourcesRequest { Party = Party }, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
    }

    private static GetAuthorizedResourcesHandler CreateHandler(
        HybridCacheTestScope cache,
        Mock<IResourceRepository> resourceRepository,
        Mock<IAuthorizationService> authorizationService,
        Mock<IAltinnResourceRepository> altinnResourceRepository,
        Mock<IServiceOwnerRepository>? serviceOwnerRepository = null)
    {
        return new(
            authorizationService.Object,
            resourceRepository.Object,
            (serviceOwnerRepository ?? new Mock<IServiceOwnerRepository>()).Object,
            altinnResourceRepository.Object,
            cache.Cache,
            NullLogger<GetAuthorizedResourcesHandler>.Instance);
    }

    private static Mock<IResourceRepository> CreateResourceRepository(params (string Id, string Owner)[] resources)
    {
        var resourceRepository = new Mock<IResourceRepository>();
        resourceRepository
            .Setup(repository => repository.GetResources(It.IsAny<CancellationToken>()))
            .ReturnsAsync(resources
                .Select(resource => new ResourceEntity { Id = resource.Id, ServiceOwnerId = $"0192:{resource.Owner}" })
                .ToList());
        return resourceRepository;
    }

    private static Mock<IAltinnResourceRepository> CreateAltinnResourceRepository(params (string ResourceId, string Title, string ServiceOwnerName)[] resources)
    {
        var altinnResourceRepository = new Mock<IAltinnResourceRepository>();
        foreach (var resource in resources)
        {
            altinnResourceRepository
                .Setup(repository => repository.GetResourceMetadata(resource.ResourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AltinnResourceMetadata
                {
                    Title = resource.Title,
                    ServiceOwnerName = resource.ServiceOwnerName
                });
        }
        return altinnResourceRepository;
    }

    private static void SetupAccessListMembership(
        Mock<IAltinnResourceRepository> altinnResourceRepository,
        string resourceId,
        string party,
        bool member)
    {
        altinnResourceRepository
            .Setup(repository => repository.GetAccessListOfResource(resourceId, party, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member ? [party] : []);
    }

    private static ClaimsPrincipal CreateUser()
        => new(new ClaimsIdentity([new Claim("urn:altinn:userid", "12345")], "Test"));
}
