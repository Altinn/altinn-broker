namespace Altinn.Broker.Core.Models.Enums
{
    public enum NotificationTemplate
    {
        /// <summary>
        /// Sent to recipients identified by organization number. $recipientName$/$recipientNumber$ are resolved by
        /// Altinn Notifications.
        /// </summary>
        GenericAltinnMessage = 0,

        /// <summary>
        /// Sent to custom recipients identified by email address or mobile number. Contains no $recipientName$ or
        /// $recipientNumber$ tokens, since Altinn Notifications has no registered name/number to resolve them to.
        /// </summary>
        GenericAltinnMessageWithoutRecipientTokens = 1,

        /// <summary>
        /// Sent to custom recipients identified by national identity number. Contains $recipientName$ (resolved by
        /// Altinn Notifications) but no $recipientNumber$, since there is no number worth showing for a person.
        /// </summary>
        GenericAltinnMessageForPerson = 2,
    }
}
