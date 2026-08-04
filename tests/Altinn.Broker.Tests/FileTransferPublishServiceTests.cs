using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Tests.Helpers;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class FileTransferPublishServiceTests
{
    [Fact]
    public async Task TryPublishAsync_WhenClaimSucceeds_InsertsPublishedAndEnqueuesStableEvents()
    {
        // Arrange
        var fileTransfer = CreateFileTransfer(recipientCount: 2);
        var timestamp = DateTimeOffset.UtcNow;
        var claimKey = FileTransferPublishService.GetPublishedClaimKey(fileTransfer.FileTransferId);

        var statusRepository = new Mock<IFileTransferStatusRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var capturedJobs = new List<Job>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        statusRepository
            .Setup(r => r.InsertFileTransferStatus(
                fileTransfer.FileTransferId,
                FileTransferStatus.Published,
                timestamp,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, _) => capturedJobs.Add(job))
            .Returns("job-id");

        var service = CreateService(statusRepository, idempotencyRepository, backgroundJobClient);

        // Act
        var published = await service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None);

        // Assert
        Assert.True(published);
        idempotencyRepository.Verify(
            r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()),
            Times.Once);
        statusRepository.Verify(
            r => r.InsertFileTransferStatus(
                fileTransfer.FileTransferId,
                FileTransferStatus.Published,
                timestamp,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Sender + 2 recipients
        Assert.Equal(3, capturedJobs.Count);
        Assert.All(capturedJobs, job =>
        {
            Assert.Equal(typeof(EventBusMiddleware), job.Type);
            Assert.Equal(nameof(EventBusMiddleware.Publish), job.Method.Name);
            Assert.Equal(AltinnEventType.Published, job.Args[0]);
            Assert.Equal(fileTransfer.ResourceId, job.Args[1]);
            Assert.Equal(fileTransfer.FileTransferId.ToString(), job.Args[2]);
        });

        var senderJob = capturedJobs[0];
        Assert.Equal(fileTransfer.Sender.ActorExternalId, senderJob.Args[3]);
        Assert.Equal(
            FileTransferPublishService.CreateStablePublishedEventId(
                fileTransfer.FileTransferId,
                AltinnEventSubjectRole.Sender,
                fileTransfer.Sender.ActorExternalId),
            senderJob.Args[4]);
        Assert.Equal(AltinnEventSubjectRole.Sender, senderJob.Args[5]);

        for (var i = 0; i < fileTransfer.RecipientCurrentStatuses.Count; i++)
        {
            var recipient = fileTransfer.RecipientCurrentStatuses[i];
            var recipientJob = capturedJobs[i + 1];
            Assert.Equal(recipient.Actor.ActorExternalId, recipientJob.Args[3]);
            Assert.Equal(
                FileTransferPublishService.CreateStablePublishedEventId(
                    fileTransfer.FileTransferId,
                    AltinnEventSubjectRole.Recipient,
                    recipient.Actor.ActorExternalId),
                recipientJob.Args[4]);
            Assert.Equal(AltinnEventSubjectRole.Recipient, recipientJob.Args[5]);
        }
    }

    [Fact]
    public async Task TryPublishAsync_WhenClaimAlreadyTaken_DoesNotInsertStatusOrEnqueueEvents()
    {
        // Arrange
        var fileTransfer = CreateFileTransfer(recipientCount: 1);
        var claimKey = FileTransferPublishService.GetPublishedClaimKey(fileTransfer.FileTransferId);

        var statusRepository = new Mock<IFileTransferStatusRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(statusRepository, idempotencyRepository, backgroundJobClient);

        // Act
        var published = await service.TryPublishAsync(fileTransfer, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        Assert.False(published);
        statusRepository.Verify(
            r => r.InsertFileTransferStatus(
                It.IsAny<Guid>(),
                It.IsAny<FileTransferStatus>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        backgroundJobClient.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task TryPublishAsync_WhenCalledTwice_OnlyFirstCallPublishes()
    {
        // Arrange
        var fileTransfer = CreateFileTransfer(recipientCount: 1);
        var claimKey = FileTransferPublishService.GetPublishedClaimKey(fileTransfer.FileTransferId);
        var claimTaken = false;

        var statusRepository = new Mock<IFileTransferStatusRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (claimTaken)
                {
                    return false;
                }

                claimTaken = true;
                return true;
            });
        statusRepository
            .Setup(r => r.InsertFileTransferStatus(
                It.IsAny<Guid>(),
                It.IsAny<FileTransferStatus>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var service = CreateService(statusRepository, idempotencyRepository, backgroundJobClient);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var first = await service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None);
        var second = await service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None);

        // Assert
        Assert.True(first);
        Assert.False(second);
        statusRepository.Verify(
            r => r.InsertFileTransferStatus(
                fileTransfer.FileTransferId,
                FileTransferStatus.Published,
                timestamp,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        // Sender + 1 recipient from the winning call only
        backgroundJobClient.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TryPublishAsync_WhenStatusInsertFails_DoesNotEnqueueAndPropagates()
    {
        // Arrange
        var fileTransfer = CreateFileTransfer(recipientCount: 1);
        var claimKey = FileTransferPublishService.GetPublishedClaimKey(fileTransfer.FileTransferId);
        var claimTaken = false;

        var statusRepository = new Mock<IFileTransferStatusRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // Simulates transactional rollback of the claim after a failed attempt:
                // the next call can claim again and recover.
                if (claimTaken)
                {
                    return false;
                }

                claimTaken = true;
                return true;
            });
        statusRepository
            .Setup(r => r.InsertFileTransferStatus(
                It.IsAny<Guid>(),
                It.IsAny<FileTransferStatus>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("status persistence failed"));

        var service = CreateService(statusRepository, idempotencyRepository, backgroundJobClient);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var firstAttempt = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None));

        // Recover: claim is available again (transaction rolled back), status succeeds.
        claimTaken = false;
        statusRepository
            .Setup(r => r.InsertFileTransferStatus(
                It.IsAny<Guid>(),
                It.IsAny<FileTransferStatus>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var recovered = await service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None);

        // Assert
        Assert.Equal("status persistence failed", firstAttempt.Message);
        backgroundJobClient.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Exactly(2));
        Assert.True(recovered);
        statusRepository.Verify(
            r => r.InsertFileTransferStatus(
                fileTransfer.FileTransferId,
                FileTransferStatus.Published,
                timestamp,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TryPublishAsync_WhenSenderEnqueueFails_PropagatesAndAllowsRetry()
    {
        // Arrange
        var fileTransfer = CreateFileTransfer(recipientCount: 1);
        var claimKey = FileTransferPublishService.GetPublishedClaimKey(fileTransfer.FileTransferId);
        var claimTaken = false;
        var enqueueAttempts = 0;

        var statusRepository = new Mock<IFileTransferStatusRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (claimTaken)
                {
                    return false;
                }

                claimTaken = true;
                return true;
            });
        statusRepository
            .Setup(r => r.InsertFileTransferStatus(
                It.IsAny<Guid>(),
                It.IsAny<FileTransferStatus>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() =>
            {
                enqueueAttempts++;
                if (enqueueAttempts == 1)
                {
                    throw new BackgroundJobClientException("sender enqueue failed", new Exception("inner"));
                }

                return $"job-{enqueueAttempts}";
            });

        var service = CreateService(statusRepository, idempotencyRepository, backgroundJobClient);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await Assert.ThrowsAsync<BackgroundJobClientException>(() =>
            service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None));

        claimTaken = false;
        var recovered = await service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None);

        // Assert
        Assert.True(recovered);
        // Failed sender enqueue + successful sender + recipient
        Assert.Equal(3, enqueueAttempts);
        statusRepository.Verify(
            r => r.InsertFileTransferStatus(
                fileTransfer.FileTransferId,
                FileTransferStatus.Published,
                timestamp,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TryPublishAsync_WhenRecipientEnqueueFails_PropagatesAndAllowsRetry()
    {
        // Arrange
        var fileTransfer = CreateFileTransfer(recipientCount: 1);
        var claimKey = FileTransferPublishService.GetPublishedClaimKey(fileTransfer.FileTransferId);
        var claimTaken = false;
        var enqueueAttempts = 0;

        var statusRepository = new Mock<IFileTransferStatusRepository>();
        var idempotencyRepository = new Mock<IIdempotencyEventRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();

        idempotencyRepository
            .Setup(r => r.TryAddIdempotencyEventAsync(claimKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (claimTaken)
                {
                    return false;
                }

                claimTaken = true;
                return true;
            });
        statusRepository
            .Setup(r => r.InsertFileTransferStatus(
                It.IsAny<Guid>(),
                It.IsAny<FileTransferStatus>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() =>
            {
                enqueueAttempts++;
                if (enqueueAttempts == 2)
                {
                    throw new BackgroundJobClientException("recipient enqueue failed", new Exception("inner"));
                }

                return $"job-{enqueueAttempts}";
            });

        var service = CreateService(statusRepository, idempotencyRepository, backgroundJobClient);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await Assert.ThrowsAsync<BackgroundJobClientException>(() =>
            service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None));

        claimTaken = false;
        var recovered = await service.TryPublishAsync(fileTransfer, timestamp, CancellationToken.None);

        // Assert
        Assert.True(recovered);
        // Failed attempt: sender ok + recipient fail; retry: sender + recipient
        Assert.Equal(4, enqueueAttempts);
        statusRepository.Verify(
            r => r.InsertFileTransferStatus(
                fileTransfer.FileTransferId,
                FileTransferStatus.Published,
                timestamp,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public void GetPublishedClaimKey_UsesFileTransferIdAndSuffix()
    {
        var fileTransferId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var key = FileTransferPublishService.GetPublishedClaimKey(fileTransferId);

        Assert.Equal($"{fileTransferId}_published", key);
    }

    [Fact]
    public void CreateStablePublishedEventId_IsDeterministicForSameInputs()
    {
        var fileTransferId = Guid.NewGuid();
        var actorExternalId = "0192:991825827";

        var first = FileTransferPublishService.CreateStablePublishedEventId(
            fileTransferId,
            AltinnEventSubjectRole.Sender,
            actorExternalId);
        var second = FileTransferPublishService.CreateStablePublishedEventId(
            fileTransferId,
            AltinnEventSubjectRole.Sender,
            actorExternalId);

        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateStablePublishedEventId_DiffersByRoleAndActor()
    {
        var fileTransferId = Guid.NewGuid();
        var senderId = "0192:991825827";
        var recipientId = "0192:986252932";

        var senderEventId = FileTransferPublishService.CreateStablePublishedEventId(
            fileTransferId,
            AltinnEventSubjectRole.Sender,
            senderId);
        var recipientEventId = FileTransferPublishService.CreateStablePublishedEventId(
            fileTransferId,
            AltinnEventSubjectRole.Recipient,
            recipientId);
        var otherRecipientEventId = FileTransferPublishService.CreateStablePublishedEventId(
            fileTransferId,
            AltinnEventSubjectRole.Recipient,
            senderId);

        Assert.NotEqual(senderEventId, recipientEventId);
        Assert.NotEqual(recipientEventId, otherRecipientEventId);
        Assert.NotEqual(senderEventId, otherRecipientEventId);
    }

    private static FileTransferPublishService CreateService(
        Mock<IFileTransferStatusRepository> statusRepository,
        Mock<IIdempotencyEventRepository> idempotencyRepository,
        Mock<IBackgroundJobClient> backgroundJobClient)
    {
        var eventBus = new Mock<IEventBus>();
        var eventBusMiddleware = new EventBusMiddleware(eventBus.Object);
        var logger = new Mock<ILogger<FileTransferPublishService>>();

        return new FileTransferPublishService(
            statusRepository.Object,
            idempotencyRepository.Object,
            backgroundJobClient.Object,
            eventBusMiddleware,
            logger.Object);
    }

    private static FileTransferEntity CreateFileTransfer(int recipientCount)
    {
        var fileTransferId = Guid.NewGuid();
        var recipients = Enumerable.Range(0, recipientCount)
            .Select(i => new ActorFileTransferStatusEntity
            {
                FileTransferId = fileTransferId,
                Date = DateTime.UtcNow,
                Status = ActorFileTransferStatus.Initialized,
                Actor = new ActorEntity
                {
                    ActorExternalId = $"0192:98625293{i}"
                }
            })
            .ToList();

        return new FileTransferEntity
        {
            FileTransferId = fileTransferId,
            ResourceId = TestConstants.RESOURCE_FOR_TEST,
            FileName = "input.txt",
            Sender = new ActorEntity
            {
                ActorExternalId = "0192:991825827"
            },
            Created = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddHours(1),
            RecipientCurrentStatuses = recipients,
            FileTransferStatusEntity = new FileTransferStatusEntity
            {
                FileTransferId = fileTransferId,
                Date = DateTime.UtcNow,
                Status = FileTransferStatus.UploadProcessing
            }
        };
    }
}
