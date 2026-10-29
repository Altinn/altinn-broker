namespace Altinn.Broker.Core.Models.Enums;

public static class NotificationStatusV2Extensions
{
    public static bool IsFailed(this NotificationStatusV2 status) => status switch
    {
        >= NotificationStatusV2.Email_Failed and <= NotificationStatusV2.Email_Failed_TTL => true,
        >= NotificationStatusV2.SMS_Failed and <= NotificationStatusV2.SMS_Failed_TTL => true,
        _ => false
    };

    public static bool IsTtlFailure(this NotificationStatusV2 status) => status switch
    {
        NotificationStatusV2.Email_Failed_TTL => true,
        NotificationStatusV2.SMS_Failed_TTL => true,
        _ => false
    };
}
