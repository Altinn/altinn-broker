namespace Altinn.Broker.Application.CheckNotificationCondition;

/// <summary>
/// The response Altinn Notification expects from a condition endpoint. The property name is part of their contract and
/// must not be renamed: a body they cannot deserialize into a boolean is treated as a failure, and they then send the
/// notification anyway.
/// </summary>
public class CheckNotificationConditionResponse
{
    public required bool SendNotification { get; set; }
}
