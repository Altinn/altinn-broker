namespace Altinn.Broker.Core.Services;

/// <summary>
/// Altinn Notifications refused to create the order (422), e.g. because the recipient has no contact information.
/// Unlike other failures this is not worth retrying - the same order would be refused again.
/// </summary>
public class NotificationOrderRejectedException(string reason) : Exception($"Altinn Notifications rejected the notification order: {reason}")
{
    /// <summary>
    /// Altinn Notifications' own explanation, kept short enough to store with the notification.
    /// </summary>
    public string Reason { get; } = reason;
}
