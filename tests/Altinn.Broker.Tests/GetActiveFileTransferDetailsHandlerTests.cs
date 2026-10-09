using System.Security.Claims;

using Altinn.Broker.Application;
using Altinn.Broker.Application.GetActiveFileTransferDetails;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class GetActiveFileTransferDetailsHandlerTests
{
    private const string CallerOrganizationNumber = "991825827";
    private const string CallerExternalId = $"0192:{CallerOrganizationNumber}";
    private const string SenderExternalId = "0192:111111111";
    private const string ResourceId = "resource-a";

    [Fact]
    public async Task Process_FileTransferNotFound_ReturnsFileTransferNotFoundError()
    {
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransfer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileTransferEntity?)null);
        var handler = CreateHandler(
            fileTransferRepository: fileTransferRepository,
            authorizationService: new Mock<IAuthorizationService>(MockBehavior.Strict),
            actorRepository: new Mock<IActorRepository>(MockBehavior.Strict));

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = Guid.NewGuid(), OnBehalfOf = CallerOrganizationNumber }, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.FileTransferNotFound, result.AsT1);
    }

    [Fact]
    public async Task Process_NoCallerResolvable_ReturnsNoAccessError()
    {
        var fileTransferRepository = CreateFileTransferRepository(CreateFileTransfer());
        var handler = CreateHandler(
            fileTransferRepository: fileTransferRepository,
            authorizationService: new Mock<IAuthorizationService>(MockBehavior.Strict),
            actorRepository: new Mock<IActorRepository>(MockBehavior.Strict));
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = Guid.NewGuid(), OnBehalfOf = string.Empty }, user, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_CallerActorNotFound_ReturnsNoAccessError()
    {
        var fileTransferRepository = CreateFileTransferRepository(CreateFileTransfer());
        var actorRepository = new Mock<IActorRepository>();
        actorRepository
            .Setup(repository => repository.GetActorAsync(CallerExternalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActorEntity?)null);
        var handler = CreateHandler(
            fileTransferRepository: fileTransferRepository,
            authorizationService: new Mock<IAuthorizationService>(MockBehavior.Strict),
            actorRepository: actorRepository);

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = Guid.NewGuid(), OnBehalfOf = CallerOrganizationNumber }, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_NoAccessToResource_ReturnsNoAccessError()
    {
        var fileTransferRepository = CreateFileTransferRepository(CreateFileTransfer());
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.CheckAccessForSearch(null, ResourceId, CallerOrganizationNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateHandler(
            fileTransferRepository: fileTransferRepository,
            authorizationService: authorizationService,
            resourceRepository: new Mock<IResourceRepository>(MockBehavior.Strict),
            altinnResourceRepository: new Mock<IAltinnResourceRepository>(MockBehavior.Strict),
            altinnRegisterService: new Mock<IAltinnRegisterService>(MockBehavior.Strict),
            fileTransferStatusRepository: new Mock<IFileTransferStatusRepository>(MockBehavior.Strict));

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = Guid.NewGuid(), OnBehalfOf = CallerOrganizationNumber }, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_CallerIsSender_SetsIsSenderTrueAndAwaitsRecipients()
    {
        var fileTransfer = CreateFileTransfer(senderExternalId: CallerExternalId, recipientStatuses: []);
        var fileTransferRepository = CreateFileTransferRepository(fileTransfer);
        var handler = CreateHandler(fileTransferRepository: fileTransferRepository);

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = fileTransfer.FileTransferId, OnBehalfOf = CallerOrganizationNumber }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        var response = result.AsT0;
        Assert.True(response.IsSender);
        Assert.Null(response.ActorDownloadStatus);
        Assert.Equal("AwaitingRecipients", response.Status);
    }

    [Theory]
    [InlineData(ActorFileTransferStatus.Initialized, "AwaitingDownloadByCurrentActor")]
    [InlineData(ActorFileTransferStatus.DownloadStarted, "AwaitingDownloadByCurrentActor")]
    [InlineData(ActorFileTransferStatus.DownloadConfirmed, "AwaitingOtherRecipients")]
    public async Task Process_CallerIsRecipient_DerivesStatusFromActorDownloadStatus(ActorFileTransferStatus actorStatus, string expectedStatus)
    {
        var recipientStatus = new ActorFileTransferStatusEntity
        {
            FileTransferId = Guid.NewGuid(),
            Actor = new ActorEntity { ActorId = 2, ActorExternalId = CallerExternalId },
            Status = actorStatus,
            Date = DateTimeOffset.UtcNow,
        };
        var fileTransfer = CreateFileTransfer(recipientStatuses: [recipientStatus]);
        var fileTransferRepository = CreateFileTransferRepository(fileTransfer);
        var handler = CreateHandler(fileTransferRepository: fileTransferRepository);

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = fileTransfer.FileTransferId, OnBehalfOf = CallerOrganizationNumber }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        var response = result.AsT0;
        Assert.False(response.IsSender);
        Assert.Equal(actorStatus.ToString(), response.ActorDownloadStatus);
        Assert.Equal(expectedStatus, response.Status);
    }

    [Theory]
    [InlineData(FileTransferStatus.Cancelled, "Cancelled")]
    [InlineData(FileTransferStatus.Purged, "Purged")]
    [InlineData(FileTransferStatus.Failed, "Failed")]
    [InlineData(FileTransferStatus.AllConfirmedDownloaded, "AllDownloaded")]
    public async Task Process_TerminalOverallStatus_TakesPrecedenceOverActorDownloadStatus(FileTransferStatus overallStatus, string expectedStatus)
    {
        var recipientStatus = new ActorFileTransferStatusEntity
        {
            FileTransferId = Guid.NewGuid(),
            Actor = new ActorEntity { ActorId = 2, ActorExternalId = CallerExternalId },
            Status = ActorFileTransferStatus.Initialized,
            Date = DateTimeOffset.UtcNow,
        };
        var fileTransfer = CreateFileTransfer(overallStatus: overallStatus, recipientStatuses: [recipientStatus]);
        var fileTransferRepository = CreateFileTransferRepository(fileTransfer);
        var handler = CreateHandler(fileTransferRepository: fileTransferRepository);

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = fileTransfer.FileTransferId, OnBehalfOf = CallerOrganizationNumber }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(expectedStatus, result.AsT0.Status);
    }

    [Fact]
    public async Task Process_OnBehalfOfProvided_IsUsedInsteadOfUserClaims()
    {
        var fileTransfer = CreateFileTransfer(senderExternalId: CallerExternalId, recipientStatuses: []);
        var fileTransferRepository = CreateFileTransferRepository(fileTransfer);
        string? authorizedParty = null;
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.CheckAccessForSearch(It.IsAny<ClaimsPrincipal?>(), ResourceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimsPrincipal?, string, string, CancellationToken>((_, _, party, _) => authorizedParty = party)
            .ReturnsAsync(true);
        var handler = CreateHandler(fileTransferRepository: fileTransferRepository, authorizationService: authorizationService);
        var user = TestTokenHelper.CreateAltinnUser("999999999"); // different org - should be ignored when OnBehalfOf is set

        var result = await handler.Process(
            new GetActiveFileTransferDetailsRequest { FileTransferId = fileTransfer.FileTransferId, OnBehalfOf = CallerOrganizationNumber }, user, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(CallerOrganizationNumber, authorizedParty);
    }

    private static GetActiveFileTransferDetailsHandler CreateHandler(
        Mock<IFileTransferRepository>? fileTransferRepository = null,
        Mock<IAuthorizationService>? authorizationService = null,
        Mock<IActorRepository>? actorRepository = null,
        Mock<IAltinnRegisterService>? altinnRegisterService = null,
        Mock<IFileTransferStatusRepository>? fileTransferStatusRepository = null,
        Mock<IResourceRepository>? resourceRepository = null,
        Mock<IAltinnResourceRepository>? altinnResourceRepository = null)
    {
        actorRepository ??= CreateActorRepository();
        authorizationService ??= CreateAuthorizationService();
        altinnRegisterService ??= CreateAltinnRegisterService();
        fileTransferStatusRepository ??= CreateFileTransferStatusRepository();
        resourceRepository ??= CreateResourceRepository();
        altinnResourceRepository ??= CreateAltinnResourceRepository();

        return new GetActiveFileTransferDetailsHandler(
            authorizationService.Object,
            fileTransferRepository!.Object,
            actorRepository.Object,
            altinnRegisterService.Object,
            fileTransferStatusRepository.Object,
            resourceRepository.Object,
            altinnResourceRepository.Object,
            NullLogger<GetActiveFileTransferDetailsHandler>.Instance);
    }

    private static Mock<IFileTransferRepository> CreateFileTransferRepository(FileTransferEntity fileTransfer)
    {
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransfer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTransfer);
        return fileTransferRepository;
    }

    private static Mock<IActorRepository> CreateActorRepository()
    {
        var actorRepository = new Mock<IActorRepository>();
        actorRepository
            .Setup(repository => repository.GetActorAsync(CallerExternalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorEntity { ActorId = 1, ActorExternalId = CallerExternalId });
        return actorRepository;
    }

    private static Mock<IAuthorizationService> CreateAuthorizationService()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.CheckAccessForSearch(It.IsAny<ClaimsPrincipal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return authorizationService;
    }

    private static Mock<IAltinnRegisterService> CreateAltinnRegisterService()
    {
        var altinnRegisterService = new Mock<IAltinnRegisterService>();
        altinnRegisterService
            .Setup(service => service.LookupOrganizationName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        return altinnRegisterService;
    }

    private static Mock<IFileTransferStatusRepository> CreateFileTransferStatusRepository()
    {
        var fileTransferStatusRepository = new Mock<IFileTransferStatusRepository>();
        fileTransferStatusRepository
            .Setup(repository => repository.GetFileTransferStatusHistory(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return fileTransferStatusRepository;
    }

    private static Mock<IResourceRepository> CreateResourceRepository()
    {
        var resourceRepository = new Mock<IResourceRepository>();
        resourceRepository
            .Setup(repository => repository.GetResource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceEntity { Id = ResourceId, ServiceOwnerId = "0192:991825827" });
        return resourceRepository;
    }

    private static Mock<IAltinnResourceRepository> CreateAltinnResourceRepository()
    {
        var altinnResourceRepository = new Mock<IAltinnResourceRepository>();
        altinnResourceRepository
            .Setup(repository => repository.GetResourceMetadata(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AltinnResourceMetadata?)null);
        return altinnResourceRepository;
    }

    private static FileTransferEntity CreateFileTransfer(
        string senderExternalId = SenderExternalId,
        FileTransferStatus overallStatus = FileTransferStatus.Published,
        List<ActorFileTransferStatusEntity>? recipientStatuses = null,
        long fileTransferSize = 0)
        => new()
        {
            FileTransferId = Guid.NewGuid(),
            ResourceId = ResourceId,
            Sender = new ActorEntity { ActorId = 3, ActorExternalId = senderExternalId },
            FileTransferStatusEntity = new FileTransferStatusEntity
            {
                Status = overallStatus,
                Date = DateTimeOffset.UtcNow,
            },
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.AddDays(30),
            RecipientCurrentStatuses = recipientStatuses ?? [],
            FileName = "test-file.pdf",
            FileTransferSize = fileTransferSize,
            UseVirusScan = false,
            PropertyList = [],
        };
}
