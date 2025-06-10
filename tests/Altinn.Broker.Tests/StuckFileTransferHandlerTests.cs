using Altinn.Broker.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;

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
        var slackClient = new Mock<ISlackClient>();
        var monitorLogger = new Mock<ILogger<StuckFileTransferHandler>>();
        var notifierLogger = new Mock<ILogger<SlackStuckFileTransferNotifier>>();
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns("Test");
        var slackSettings = new SlackSettings(hostEnvironment.Object);
        var slackNotifier = new SlackStuckFileTransferNotifier(notifierLogger.Object, slackClient.Object, hostEnvironment.Object, slackSettings);
        var handler = new StuckFileTransferHandler(fileTransferStatusRepository.Object, slackNotifier, monitorLogger.Object);
        var cancellationToken = new CancellationToken();
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(It.IsAny<Core.Domain.Enums.FileTransferStatus>(), It.IsAny<DateTime>(), cancellationToken))
            .ReturnsAsync(new List<FileTransferStatusEntity>());

        // Act
        await handler.CheckForStuckFileTransfers(cancellationToken);

        // Assert
        monitorLogger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString() == "Checking for file transfers stuck in upload processing"),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CheckForStuckFileTransfers_WhenStuckFileTransfers_SendsSlackNotification()
    {
        // Arrange
        var fileTransferStatusRepository = new Mock<IFileTransferStatusRepository>();
        var slackClient = new Mock<ISlackClient>();
        var monitorLogger = new Mock<ILogger<StuckFileTransferHandler>>();
        var notifierLogger = new Mock<ILogger<SlackStuckFileTransferNotifier>>();
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns("Test");
        var slackSettings = new SlackSettings(hostEnvironment.Object);
        var slackNotifier = new SlackStuckFileTransferNotifier(notifierLogger.Object, slackClient.Object, hostEnvironment.Object, slackSettings);
        var handler = new StuckFileTransferHandler(fileTransferStatusRepository.Object, slackNotifier, monitorLogger.Object);
        var cancellationToken = new CancellationToken();
        var fileTransferId = Guid.NewGuid();
        var stuckfileTransferStatuses = new List<FileTransferStatusEntity>()
        {
            new FileTransferStatusEntity()
            {
                FileTransferId = fileTransferId,
                Status = Core.Domain.Enums.FileTransferStatus.UploadProcessing,
                Date = DateTime.UtcNow.AddMinutes(-16)
            }
        };
        fileTransferStatusRepository.Setup(r => r
            .GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(It.IsAny<Core.Domain.Enums.FileTransferStatus>(), It.IsAny<DateTime>(), cancellationToken))
            .ReturnsAsync(stuckfileTransferStatuses);

        // Act
        await handler.CheckForStuckFileTransfers(cancellationToken);

        // Assert
        monitorLogger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString() == $"File transfer {fileTransferId} has been stuck in upload processing for more than 15 minutes"),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);

        slackClient.Verify(s => s.PostAsync(It.Is<SlackMessage>(m => 
            m.Text.Contains(fileTransferId.ToString()) && 
            m.Text.Contains("UploadProcessing"))), 
            Times.Once);
    }
}