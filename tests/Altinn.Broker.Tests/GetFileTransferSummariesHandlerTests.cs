using System.Security.Claims;

using Altinn.Broker.Application;
using Altinn.Broker.Application.GetFileTransferSummaries;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class GetFileTransferSummariesHandlerTests
{
    private const string OrganizationNumber = "991825827";

    [Fact]
    public async Task Process_NoCallerResolvable_ReturnsNoAccessError()
    {
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var fileTransferRepository = new Mock<IFileTransferRepository>(MockBehavior.Strict);
        var actorRepository = new Mock<IActorRepository>(MockBehavior.Strict);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await handler.Process(new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], View = FileTransferListView.Active }, user, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_ActorNotFound_ReturnsEmptyListWithoutCallingAuthorizationOrRepository()
    {
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var fileTransferRepository = new Mock<IFileTransferRepository>(MockBehavior.Strict);
        var actorRepository = new Mock<IActorRepository>();
        actorRepository
            .Setup(repository => repository.GetActorAsync($"0192:{OrganizationNumber}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActorEntity?)null);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], OnBehalfOf = OrganizationNumber, View = FileTransferListView.Active }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
    }

    [Fact]
    public async Task Process_NoAuthorizedResources_ReturnsEmptyListWithoutQueryingRepository()
    {
        var actor = new ActorEntity { ActorId = 1, ActorExternalId = $"0192:{OrganizationNumber}" };
        var actorRepository = CreateActorRepository(actor);
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(null, OrganizationNumber, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AuthorizedResource { ResourceId = "resource-a" }]); // CanSend/CanReceive both false
        var fileTransferRepository = new Mock<IFileTransferRepository>(MockBehavior.Strict);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], OnBehalfOf = OrganizationNumber, View = FileTransferListView.Active }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
    }

    [Fact]
    public async Task Process_FiltersRepositoryQueryToOnlyAuthorizedResourceIds()
    {
        var actor = new ActorEntity { ActorId = 1, ActorExternalId = $"0192:{OrganizationNumber}" };
        var actorRepository = CreateActorRepository(actor);
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(null, OrganizationNumber, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AuthorizedResource { ResourceId = "resource-a", CanSend = true },
                new AuthorizedResource { ResourceId = "resource-b" } // no access
            ]);
        List<string>? queriedResourceIds = null;
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransferSummariesAssociatedWithActor(It.IsAny<FrontendFileTransferSearchEntity>(), It.IsAny<CancellationToken>()))
            .Callback<FrontendFileTransferSearchEntity, CancellationToken>((search, _) => queriedResourceIds = search.ResourceIds)
            .ReturnsAsync([]);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a", "resource-b"], OnBehalfOf = OrganizationNumber, View = FileTransferListView.Active }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(["resource-a"], queriedResourceIds);
    }

    [Fact]
    public async Task Process_EnrichesSenderAndRecipientsWithOrganizationNames_FallingBackToOriginalIdWhenLookupFails()
    {
        var actor = new ActorEntity { ActorId = 1, ActorExternalId = $"0192:{OrganizationNumber}" };
        var actorRepository = CreateActorRepository(actor);
        var authorizationService = CreateAuthorizationService("resource-a");
        var summary = new FileTransferSummaryEntity
        {
            FileTransferId = Guid.NewGuid(),
            ResourceId = "resource-a",
            Sender = "0192:111111111",
            Recipients = ["0192:222222222", "0192:333333333"],
            SendersFileTransferReference = "ref-1"
        };
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransferSummariesAssociatedWithActor(It.IsAny<FrontendFileTransferSearchEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([summary]);
        var altinnRegisterService = new Mock<IAltinnRegisterService>();
        altinnRegisterService.Setup(service => service.LookupOrganizationName("0192:111111111", It.IsAny<CancellationToken>())).ReturnsAsync("Sender AS");
        altinnRegisterService.Setup(service => service.LookupOrganizationName("0192:222222222", It.IsAny<CancellationToken>())).ReturnsAsync("Recipient One AS");
        altinnRegisterService.Setup(service => service.LookupOrganizationName("0192:333333333", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], OnBehalfOf = OrganizationNumber, View = FileTransferListView.Active }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        var resultSummary = Assert.Single(result.AsT0);
        Assert.Equal("Sender AS", resultSummary.Sender);
        Assert.Equal(["Recipient One AS", "0192:333333333"], resultSummary.Recipients);
    }

    [Fact]
    public async Task Process_OnBehalfOfProvided_IsUsedInsteadOfUserClaims()
    {
        const string onBehalfOfOrg = "912345678";
        var actor = new ActorEntity { ActorId = 2, ActorExternalId = $"0192:{onBehalfOfOrg}" };
        string? actorLookupArgument = null;
        var actorRepository = new Mock<IActorRepository>();
        actorRepository
            .Setup(repository => repository.GetActorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((externalId, _) => actorLookupArgument = externalId)
            .ReturnsAsync(actor);
        string? authorizedParty = null;
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimsPrincipal?, string, IReadOnlyList<string>, CancellationToken>((_, party, _, _) => authorizedParty = party)
            .ReturnsAsync([]);
        var fileTransferRepository = new Mock<IFileTransferRepository>(MockBehavior.Strict);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);
        var user = TestTokenHelper.CreateAltinnUser("999999999"); // different org - should be ignored when OnBehalfOf is set

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], OnBehalfOf = onBehalfOfOrg, View = FileTransferListView.Active }, user, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(onBehalfOfOrg, authorizedParty);
        Assert.Equal($"0192:{onBehalfOfOrg}", actorLookupArgument);
    }

    [Fact]
    public async Task Process_ActiveView_QueriesRepositoryWithOnlyPublishedStatus()
    {
        var actor = new ActorEntity { ActorId = 1, ActorExternalId = $"0192:{OrganizationNumber}" };
        var actorRepository = CreateActorRepository(actor);
        var authorizationService = CreateAuthorizationService("resource-a");
        List<FileTransferStatus>? queriedStatuses = null;
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransferSummariesAssociatedWithActor(It.IsAny<FrontendFileTransferSearchEntity>(), It.IsAny<CancellationToken>()))
            .Callback<FrontendFileTransferSearchEntity, CancellationToken>((search, _) => queriedStatuses = search.Statuses)
            .ReturnsAsync([]);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], OnBehalfOf = OrganizationNumber, View = FileTransferListView.Active }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal([FileTransferStatus.Published], queriedStatuses);
    }

    [Fact]
    public async Task Process_HistoricalView_QueriesRepositoryWithTerminalStatuses()
    {
        var actor = new ActorEntity { ActorId = 1, ActorExternalId = $"0192:{OrganizationNumber}" };
        var actorRepository = CreateActorRepository(actor);
        var authorizationService = CreateAuthorizationService("resource-a");
        List<FileTransferStatus>? queriedStatuses = null;
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransferSummariesAssociatedWithActor(It.IsAny<FrontendFileTransferSearchEntity>(), It.IsAny<CancellationToken>()))
            .Callback<FrontendFileTransferSearchEntity, CancellationToken>((search, _) => queriedStatuses = search.Statuses)
            .ReturnsAsync([]);
        var altinnRegisterService = new Mock<IAltinnRegisterService>(MockBehavior.Strict);
        var handler = CreateHandler(authorizationService, fileTransferRepository, actorRepository, altinnRegisterService);

        var result = await handler.Process(
            new GetFileTransferSummariesRequest { ResourceIds = ["resource-a"], OnBehalfOf = OrganizationNumber, View = FileTransferListView.Historical }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(
            [FileTransferStatus.Cancelled, FileTransferStatus.AllConfirmedDownloaded, FileTransferStatus.Purged, FileTransferStatus.Failed],
            queriedStatuses);
    }

    private static GetFileTransferSummariesHandler CreateHandler(
        Mock<IAuthorizationService> authorizationService,
        Mock<IFileTransferRepository> fileTransferRepository,
        Mock<IActorRepository> actorRepository,
        Mock<IAltinnRegisterService> altinnRegisterService)
        => new(
            authorizationService.Object,
            fileTransferRepository.Object,
            actorRepository.Object,
            altinnRegisterService.Object,
            NullLogger<GetFileTransferSummariesHandler>.Instance);

    private static Mock<IActorRepository> CreateActorRepository(ActorEntity actor)
    {
        var actorRepository = new Mock<IActorRepository>();
        actorRepository
            .Setup(repository => repository.GetActorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        return actorRepository;
    }

    private static Mock<IAuthorizationService> CreateAuthorizationService(params string[] authorizedResourceIds)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.GetAuthorizedResources(It.IsAny<ClaimsPrincipal?>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorizedResourceIds.Select(id => new AuthorizedResource { ResourceId = id, CanSend = true }).ToList());
        return authorizationService;
    }
}
