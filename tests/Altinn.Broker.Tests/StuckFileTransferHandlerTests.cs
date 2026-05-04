using Altinn.Broker.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using Slack.Webhooks;

using Xunit;

namespace Altinn.Broker.Tests;

public class StuckFileTransferHandlerTests
{
    [Fact]
    public async Task CheckForStuckFileTransfers_WhenCalled_LogsInformation()
    {
        // Arrange
        var fileTransferStatusRepository = new Mock<IFileTransferStatusRepository>();
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var slackClient = new Mock<ISlackClient>();
        var monitorLogger = new Mock<ILogger<StuckFileTransferHandler>>();
        var notifierLogger = new Mock<ILogger<SlackStuckFileTransferNotifier>>();
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns("Test");
        var slackSettings = new SlackSettings(hostEnvironment.Object);
        var slackNotifier = new SlackStuckFileTransferNotifier(notifierLogger.Object, slackClient.Object, hostEnvironment.Object, slackSettings);
        var handler = new StuckFileTransferHandler(fileTransferStatusRepository.Object, fileTransferRepository.Object, backgroundJobClient.Object, slackNotifier, monitorLogger.Object);
        var cancellationToken = new CancellationToken();
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(It.IsAny<List<FileTransferStatus>>(), It.IsAny<DateTime>(), cancellationToken))
            .ReturnsAsync(new List<FileTransferStatusEntity>());

        // Act
        await handler.CheckForStuckFileTransfers(cancellationToken);

        // Assert
        monitorLogger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString() == "Checking for file transfers stuck in upload started"),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CheckForStuckFileTransfers_WhenStuckFileTransfers_SendsSlackNotification()
    {
        // Arrange
        var fileTransferStatusRepository = new Mock<IFileTransferStatusRepository>();
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var slackClient = new Mock<ISlackClient>();
        var monitorLogger = new Mock<ILogger<StuckFileTransferHandler>>();
        var notifierLogger = new Mock<ILogger<SlackStuckFileTransferNotifier>>();
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns("Test");
        var slackSettings = new SlackSettings(hostEnvironment.Object);
        var slackNotifier = new SlackStuckFileTransferNotifier(notifierLogger.Object, slackClient.Object, hostEnvironment.Object, slackSettings);
        var handler = new StuckFileTransferHandler(fileTransferStatusRepository.Object, fileTransferRepository.Object, backgroundJobClient.Object, slackNotifier, monitorLogger.Object);
        var cancellationToken = new CancellationToken();
        var fileTransferId = Guid.NewGuid();
        var stuckfileTransferStatuses = new List<FileTransferStatusEntity>()
        {
            new FileTransferStatusEntity()
            {
                FileTransferId = fileTransferId,
                Status = FileTransferStatus.UploadProcessing,
                Date = DateTime.UtcNow.AddMinutes(-16)
            }
        };
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
                It.Is<List<FileTransferStatus>>(s => s.Count == 1 && s[0] == FileTransferStatus.UploadStarted),
                It.IsAny<DateTime>(),
                cancellationToken))
            .ReturnsAsync(new List<FileTransferStatusEntity>());
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
                It.Is<List<FileTransferStatus>>(s => s.Count == 1 && s[0] == FileTransferStatus.UploadProcessing),
                It.IsAny<DateTime>(),
                cancellationToken))
            .ReturnsAsync(stuckfileTransferStatuses);
        fileTransferRepository.Setup(r => r.GetFileTransfer(fileTransferId, cancellationToken)).ReturnsAsync(new FileTransferEntity()
        {
            FileTransferId = fileTransferId,
            ResourceId = "test-resource",
            Sender = new ActorEntity() { ActorExternalId = "0192:123456789" },
            Created = DateTime.UtcNow.AddHours(-1),
            ExpirationTime = DateTime.UtcNow,
            FileName = "testfile.txt",
            FileTransferStatusEntity = stuckfileTransferStatuses.First(),
            RecipientCurrentStatuses = new List<ActorFileTransferStatusEntity>()
            {
                new ActorFileTransferStatusEntity()
                {
                    Actor = new ActorEntity() {
                        ActorExternalId = "0192:987654321"
                    },
                    Status = ActorFileTransferStatus.Initialized,
                    Date = DateTime.UtcNow.AddMinutes(-16)
                }
            }
        });

        // Act
        await handler.CheckForStuckFileTransfers(cancellationToken);

        // Assert
        monitorLogger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString() == $"File transfer {fileTransferId} has been stuck in UploadProcessing for more than 15 minutes"),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);

        slackClient.Verify(s => s.PostAsync(It.Is<SlackMessage>(m => 
            m.Text.Contains(fileTransferId.ToString()) && 
            m.Text.Contains("UploadProcessing"))), 
            Times.Once);
    }

    [Fact]
    public async Task CheckForStuckFileTransfers_WhenStuckInUploadStarted_InsertsFailedAndEnqueuesUploadFailedEvent()
    {
        // Arrange
        var fileTransferStatusRepository = new Mock<IFileTransferStatusRepository>();
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var slackClient = new Mock<ISlackClient>();
        var monitorLogger = new Mock<ILogger<StuckFileTransferHandler>>();
        var notifierLogger = new Mock<ILogger<SlackStuckFileTransferNotifier>>();
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns("Test");
        var slackSettings = new SlackSettings(hostEnvironment.Object);
        var slackNotifier = new SlackStuckFileTransferNotifier(notifierLogger.Object, slackClient.Object, hostEnvironment.Object, slackSettings);
        var handler = new StuckFileTransferHandler(fileTransferStatusRepository.Object, fileTransferRepository.Object, backgroundJobClient.Object, slackNotifier, monitorLogger.Object);
        var cancellationToken = new CancellationToken();
        var fileTransferId = Guid.NewGuid();
        var stuckInUploadStartedStatuses = new List<FileTransferStatusEntity>
        {
            new()
            {
                FileTransferId = fileTransferId,
                Status = FileTransferStatus.UploadStarted,
                Date = DateTime.UtcNow.AddDays(-2)
            }
        };
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
                It.Is<List<FileTransferStatus>>(s => s.Count == 1 && s[0] == FileTransferStatus.UploadStarted),
                It.IsAny<DateTime>(),
                cancellationToken))
            .ReturnsAsync(stuckInUploadStartedStatuses);
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
                It.Is<List<FileTransferStatus>>(s => s.Count == 1 && s[0] == FileTransferStatus.UploadProcessing),
                It.IsAny<DateTime>(),
                cancellationToken))
            .ReturnsAsync(new List<FileTransferStatusEntity>());
        fileTransferStatusRepository.Setup(r => r.InsertFileTransferStatus(
                fileTransferId,
                FileTransferStatus.Failed,
                It.IsAny<DateTimeOffset>(),
                It.Is<string>(d => d.Contains("UploadStarted")),
                cancellationToken))
            .Returns(Task.CompletedTask);
        var fileTransferEntity = new FileTransferEntity
        {
            FileTransferId = fileTransferId,
            ResourceId = "test-resource",
            Sender = new ActorEntity { ActorExternalId = "0192:123456789" },
            Created = DateTime.UtcNow.AddHours(-1),
            ExpirationTime = DateTime.UtcNow,
            FileName = "testfile.txt",
            FileTransferStatusEntity = stuckInUploadStartedStatuses.First(),
            RecipientCurrentStatuses = new List<ActorFileTransferStatusEntity>()
        };
        fileTransferRepository.Setup(r => r.GetFileTransfer(fileTransferId, cancellationToken)).ReturnsAsync(fileTransferEntity);
        Job? capturedJob = null;
        backgroundJobClient.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, _) => capturedJob = job)
            .Returns("job-id");

        // Act
        await handler.CheckForStuckFileTransfers(cancellationToken);

        // Assert
        fileTransferStatusRepository.Verify(r => r.InsertFileTransferStatus(
            fileTransferId,
            FileTransferStatus.Failed,
            It.IsAny<DateTimeOffset>(),
            It.Is<string>(d => d.Contains("UploadStarted")),
            cancellationToken), Times.Once);
        backgroundJobClient.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
        Assert.NotNull(capturedJob);
        Assert.Equal(typeof(IEventBus), capturedJob!.Type);
        Assert.Equal(nameof(IEventBus.Publish), capturedJob.Method.Name);
        Assert.Equal(AltinnEventType.UploadFailed, capturedJob.Args[0]);
        Assert.Equal("test-resource", capturedJob.Args[1]);
        Assert.Equal(fileTransferId.ToString(), capturedJob.Args[2]);
        Assert.Equal("0192:123456789", capturedJob.Args[3]);
        Assert.Equal(AltinnEventSubjectRole.Sender, capturedJob.Args[5]);
        monitorLogger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(fileTransferId.ToString()) && v.ToString()!.Contains("UploadStarted")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        slackClient.Verify(s => s.PostAsync(It.IsAny<SlackMessage>()), Times.Never);
    }
}
