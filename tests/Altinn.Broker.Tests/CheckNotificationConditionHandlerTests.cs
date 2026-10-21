using Altinn.Broker.Application.CheckNotificationCondition;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class CheckNotificationConditionHandlerTests
{
    private const long RecipientActorId = 42;
    private const string RecipientOrganizationNumber = "0192:123456789";

    private static readonly Guid NotificationId = Guid.NewGuid();
    private static readonly Guid FileTransferId = Guid.NewGuid();

    private static BrokerNotificationEntity NotificationForRecipient() => new()
    {
        Id = NotificationId,
        FileTransferId = FileTransferId,
        ActorId = RecipientActorId,
        NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
        NotificationChannel = NotificationChannel.EmailPreferred,
        RequestedSendTime = DateTimeOffset.UtcNow,
        Created = DateTimeOffset.UtcNow
    };

    private static BrokerNotificationEntity NotificationForCustomRecipient() => new()
    {
        Id = NotificationId,
        FileTransferId = FileTransferId,
        ActorId = null,
        CustomRecipientType = CustomRecipientType.Email,
        CustomRecipientIdentifier = "accountant@example.com",
        CustomRecipientRelatedOrganization = RecipientOrganizationNumber,
        NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
        NotificationChannel = NotificationChannel.EmailPreferred,
        RequestedSendTime = DateTimeOffset.UtcNow,
        Created = DateTimeOffset.UtcNow
    };

    private static FileTransferEntity FileTransfer(
        ActorFileTransferStatus recipientStatus = ActorFileTransferStatus.Initialized,
        FileTransferStatus status = FileTransferStatus.Published,
        long recipientActorId = RecipientActorId,
        string recipientExternalId = RecipientOrganizationNumber) => new()
        {
            FileTransferId = FileTransferId,
            ResourceId = "resource123",
            Sender = new ActorEntity { ActorId = 1, ActorExternalId = "0192:991825827" },
            FileName = "document.pdf",
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.AddDays(30),
            FileTransferStatusEntity = new FileTransferStatusEntity
            {
                FileTransferId = FileTransferId,
                Status = status,
                Date = DateTimeOffset.UtcNow
            },
            RecipientCurrentStatuses =
            [
                new ActorFileTransferStatusEntity
                {
                    FileTransferId = FileTransferId,
                    Actor = new ActorEntity { ActorId = recipientActorId, ActorExternalId = recipientExternalId },
                    Status = recipientStatus,
                    Date = DateTimeOffset.UtcNow
                }
            ]
        };

    private static (
        Mock<IFileTransferNotificationRepository> NotificationRepository,
        Mock<IFileTransferRepository> FileTransferRepository,
        CheckNotificationConditionHandler Handler) CreateHandler(
        BrokerNotificationEntity? notification,
        FileTransferEntity? fileTransfer)
    {
        var notificationRepository = new Mock<IFileTransferNotificationRepository>();
        var fileTransferRepository = new Mock<IFileTransferRepository>();
        notificationRepository
            .Setup(r => r.GetNotificationById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        fileTransferRepository
            .Setup(r => r.GetFileTransfer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTransfer);

        var handler = new CheckNotificationConditionHandler(
            notificationRepository.Object,
            fileTransferRepository.Object,
            new Mock<ILogger<CheckNotificationConditionHandler>>().Object);

        return (notificationRepository, fileTransferRepository, handler);
    }

    private static async Task<bool> SendNotification(BrokerNotificationEntity? notification, FileTransferEntity? fileTransfer)
    {
        var (_, _, handler) = CreateHandler(notification, fileTransfer);
        var result = await handler.Process(NotificationId, null, CancellationToken.None);
        return result.AsT0.SendNotification;
    }

    [Theory]
    [InlineData(ActorFileTransferStatus.Initialized)]
    [InlineData(ActorFileTransferStatus.DownloadStarted)]
    public async Task Process_WhenRecipientHasNotConfirmedTheDownload_SendsTheNotification(ActorFileTransferStatus recipientStatus)
    {
        Assert.True(await SendNotification(NotificationForRecipient(), FileTransfer(recipientStatus)));
    }

    [Fact]
    public async Task Process_WhenRecipientHasConfirmedTheDownload_DoesNotSendTheNotification()
    {
        Assert.False(await SendNotification(NotificationForRecipient(), FileTransfer(ActorFileTransferStatus.DownloadConfirmed)));
    }

    [Fact]
    public async Task Process_ForCustomRecipient_FollowsTheRelatedOrganizationsStatus()
    {
        Assert.True(await SendNotification(NotificationForCustomRecipient(), FileTransfer(ActorFileTransferStatus.DownloadStarted)));
        Assert.False(await SendNotification(NotificationForCustomRecipient(), FileTransfer(ActorFileTransferStatus.DownloadConfirmed)));
    }

    [Fact]
    public async Task Process_ForCustomRecipient_MatchesTheRelatedOrganizationWithoutPrefix()
    {
        var fileTransfer = FileTransfer(ActorFileTransferStatus.DownloadConfirmed, recipientExternalId: "123456789");

        Assert.False(await SendNotification(NotificationForCustomRecipient(), fileTransfer));
    }

    [Fact]
    public async Task Process_WhenOneOfSeveralRecipientsHasConfirmed_OnlyStopsThatRecipientsNotification()
    {
        var fileTransfer = FileTransfer(ActorFileTransferStatus.DownloadConfirmed);
        fileTransfer.RecipientCurrentStatuses.Add(new ActorFileTransferStatusEntity
        {
            FileTransferId = FileTransferId,
            Actor = new ActorEntity { ActorId = 43, ActorExternalId = "0192:987654321" },
            Status = ActorFileTransferStatus.Initialized,
            Date = DateTimeOffset.UtcNow
        });

        var confirmedRecipient = NotificationForRecipient();
        var otherRecipient = NotificationForRecipient();
        otherRecipient.ActorId = 43;

        Assert.False(await SendNotification(confirmedRecipient, fileTransfer));
        Assert.True(await SendNotification(otherRecipient, fileTransfer));
    }

    [Theory]
    [InlineData(FileTransferStatus.Purged)]
    [InlineData(FileTransferStatus.Cancelled)]
    [InlineData(FileTransferStatus.Failed)]
    public async Task Process_WhenThereIsNoFileLeftToDownload_DoesNotSendTheNotification(FileTransferStatus status)
    {
        Assert.False(await SendNotification(NotificationForRecipient(), FileTransfer(status: status)));
    }

    [Fact]
    public async Task Process_WithUnknownNotification_DoesNotSendTheNotification()
    {
        Assert.False(await SendNotification(null, FileTransfer()));
    }

    [Fact]
    public async Task Process_WhenFileTransferNoLongerExists_DoesNotSendTheNotification()
    {
        Assert.False(await SendNotification(NotificationForRecipient(), null));
    }

    [Fact]
    public async Task Process_WhenRecipientCannotBeFoundOnTheFileTransfer_SendsTheNotification()
    {
        var fileTransfer = FileTransfer(ActorFileTransferStatus.DownloadConfirmed, recipientActorId: 999, recipientExternalId: "0192:987654321");

        Assert.True(await SendNotification(NotificationForRecipient(), fileTransfer));
    }
}
