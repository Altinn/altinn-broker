using System.Text.Json.Serialization;
using Altinn.Broker.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Core.Models.Notifications
{
    public class NotificationOrderRequestV2
    {
        public string? SendersReference { get; set; }

        public DateTime RequestedSendTime { get; set; }

        public string? ConditionEndpoint { get; set; }

        public DialogportenAssociation? DialogportenAssociation { get; set; }

        public Guid IdempotencyId { get; set; }

        [OnlyOneRecipientType]
        public RecipientV2 Recipient { get; set; } = null!;

        public List<ReminderV2>? Reminders { get; set; }
    }

    public class OnlyOneRecipientTypeAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not RecipientV2 recipient)
            {
                return new ValidationResult("Value must be of type RecipientV2");
            }

            var count = 0;
            if (recipient.RecipientOrganization != null) count++;
            if (recipient.RecipientPerson != null) count++;
            if (recipient.RecipientExternalIdentity != null) count++;
            if (recipient.RecipientEmail != null) count++;
            if (recipient.RecipientSms != null) count++;

            if (count != 1)
            {
                return new ValidationResult("Exactly one recipient type must be set");
            }

            return ValidationResult.Success;
        }
    }

    public class DialogportenAssociation
    {
        public string DialogId { get; set; } = null!;

        public string TransmissionId { get; set; } = null!;
    }

    public class RecipientV2
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientOrganization? RecipientOrganization { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientPerson? RecipientPerson { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientExternalIdentity? RecipientExternalIdentity { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientEmail? RecipientEmail { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientSms? RecipientSms { get; set; }
    }

    public class RecipientOrganization
    {
        public string OrgNumber { get; set; } = null!;

        public string ResourceId { get; set; } = null!;

        public NotificationChannel ChannelSchema { get; set; }

        public EmailSettings? EmailSettings { get; set; }

        public SmsSettings? SmsSettings { get; set; }
    }

    public class RecipientPerson
    {
        public string NationalIdentityNumber { get; set; } = null!;

        public string ResourceId { get; set; } = null!;

        public NotificationChannel ChannelSchema { get; set;}

        public EmailSettings? EmailSettings { get; set; }

        public SmsSettings? SmsSettings { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IgnoreReservation { get; set; }
    }

    public class RecipientExternalIdentity
    {
        public string ResourceId { get; set; } = null!;

        public string ExternalIdentity { get; set; } = null!;

        public NotificationChannel ChannelSchema { get; set; }

        public EmailSettings? EmailSettings { get; set; }

        public SmsSettings? SmsSettings { get; set; }
    }

    public class RecipientEmail
    {
        public string EmailAddress { get; set; } = null!;
        public EmailSettings? EmailSettings { get; set; }
    }

    public class RecipientSms
    {
        public string PhoneNumber { get; set; } = null!;
        public SmsSettings? SmsSettings { get; set; }
    }

    public class EmailSettings
    {
        public string Subject { get; set; } = null!;

        public string Body { get; set; } = null!;

        public EmailContentType ContentType { get; set; } = EmailContentType.Plain;
    }

    public class SmsSettings
    {
        public string Body { get; set; } = null!;
    }

    public class ReminderV2
    {
        public string? SendersReference { get; set; }

        public int DelayDays { get; set; }

        public string? ConditionEndpoint { get; set; }

        public RecipientV2 Recipient { get; set; } = null!;
    }
} 