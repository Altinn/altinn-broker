using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

/// <summary>
/// A published file transfer that still has notifications that were never ordered from Altinn Notifications.
/// </summary>
/// <param name="CurrentStatus">The file transfer's latest status, or null when it has not been recorded.</param>
public record FileTransferWithPendingNotifications(Guid FileTransferId, DateTimeOffset PublishedAt, FileTransferStatus? CurrentStatus);
