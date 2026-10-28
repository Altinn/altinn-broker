using System.Security.Claims;

using Altinn.Broker.Application;
using Altinn.Broker.Application.GetPartialUploads;
using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class GetPartialUploadsHandlerTests
{
    private const string SenderExternalId = "0192:991825827";
    private const string ResourceId = "resource-a";

    [Fact]
    public async Task Process_FileTransferNotFound_ReturnsFileTransferNotFoundError()
    {
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransfer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileTransferEntity?)null);
        var handler = CreateHandler(
            fileTransferRepository,
            new Mock<IAuthorizationService>(MockBehavior.Strict),
            new Mock<ITusPartialUploadLookup>(MockBehavior.Strict));

        var result = await handler.Process(new GetPartialUploadsRequest { FileTransferId = Guid.NewGuid() }, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.FileTransferNotFound, result.AsT1);
    }

    [Fact]
    public async Task Process_CallerIsNotTheSender_ReturnsNoAccessError()
    {
        var fileTransfer = CreateFileTransfer();
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.CheckAccessAsSender(
                It.IsAny<ClaimsPrincipal?>(), ResourceId, SenderExternalId, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(false);
        var handler = CreateHandler(
            CreateFileTransferRepository(fileTransfer),
            authorizationService,
            new Mock<ITusPartialUploadLookup>(MockBehavior.Strict));

        var result = await handler.Process(
            new GetPartialUploadsRequest { FileTransferId = fileTransfer.FileTransferId }, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_Sender_ReturnsThePartialUploads()
    {
        var fileTransfer = CreateFileTransfer();
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.CheckAccessAsSender(
                It.IsAny<ClaimsPrincipal?>(), ResourceId, SenderExternalId, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        IReadOnlyList<TusPartialUpload> partialUploads = [new("partial-a", 1024), new("partial-b", 512)];
        var partialUploadLookup = new Mock<ITusPartialUploadLookup>();
        partialUploadLookup
            .Setup(lookup => lookup.GetPartialUploadsAsync(fileTransfer.FileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partialUploads);
        var handler = CreateHandler(CreateFileTransferRepository(fileTransfer), authorizationService, partialUploadLookup);

        var result = await handler.Process(
            new GetPartialUploadsRequest { FileTransferId = fileTransfer.FileTransferId }, null, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(partialUploads, result.AsT0.PartialUploads);
    }

    private static GetPartialUploadsHandler CreateHandler(
        Mock<IFileTransferRepository> fileTransferRepository,
        Mock<IAuthorizationService> authorizationService,
        Mock<ITusPartialUploadLookup> partialUploadLookup)
        => new(
            authorizationService.Object,
            fileTransferRepository.Object,
            partialUploadLookup.Object,
            NullLogger<GetPartialUploadsHandler>.Instance);

    private static Mock<IFileTransferRepository> CreateFileTransferRepository(FileTransferEntity fileTransfer)
    {
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(repository => repository.GetFileTransfer(fileTransfer.FileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTransfer);
        return fileTransferRepository;
    }

    private static FileTransferEntity CreateFileTransfer()
        => new()
        {
            FileTransferId = Guid.NewGuid(),
            ResourceId = ResourceId,
            Sender = new ActorEntity { ActorId = 3, ActorExternalId = SenderExternalId },
            FileTransferStatusEntity = new FileTransferStatusEntity
            {
                Status = FileTransferStatus.UploadStarted,
                Date = DateTimeOffset.UtcNow,
            },
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.AddDays(30),
            RecipientCurrentStatuses = [],
            FileName = "test-file.pdf",
            FileTransferSize = 0,
            UseVirusScan = false,
            PropertyList = [],
        };
}
