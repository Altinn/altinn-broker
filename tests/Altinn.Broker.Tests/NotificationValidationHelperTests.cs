using Altinn.Broker.Application;
using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;

using PhoneNumbers;

using Xunit;

namespace Altinn.Broker.Tests;

public class NotificationValidationHelperTests
{
    private static readonly string ValidMobileNumber = PhoneNumberUtil.GetInstance()
        .Format(
            PhoneNumberUtil.GetInstance().GetExampleNumberForType("NO", PhoneNumberType.MOBILE),
            PhoneNumberFormat.E164);

    private static NotificationRequest CreateRequest(
        NotificationTemplate template = NotificationTemplate.CustomMessage,
        NotificationChannel channel = NotificationChannel.Email,
        string? emailSubject = "subject",
        string? emailBody = "body",
        string? smsBody = "sms",
        bool sendReminder = false,
        NotificationChannel? reminderChannel = null,
        string? reminderEmailSubject = null,
        string? reminderEmailBody = null,
        string? reminderSmsBody = null,
        List<Recipient>? customRecipients = null) => new()
    {
        NotificationTemplate = template,
        NotificationChannel = channel,
        EmailSubject = emailSubject,
        EmailBody = emailBody,
        SmsBody = smsBody,
        SendReminder = sendReminder,
        ReminderNotificationChannel = reminderChannel,
        ReminderEmailSubject = reminderEmailSubject,
        ReminderEmailBody = reminderEmailBody,
        ReminderSmsBody = reminderSmsBody,
        CustomRecipients = customRecipients
    };

    [Fact]
    public void Validate_WithCompleteCustomMessageRequest_ReturnsNull()
    {
        var request = CreateRequest();

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Theory]
    [InlineData(NotificationChannel.Email)]
    [InlineData(NotificationChannel.Sms)]
    [InlineData(NotificationChannel.EmailAndSms)]
    [InlineData(NotificationChannel.EmailPreferred)]
    [InlineData(NotificationChannel.SmsPreferred)]
    public void Validate_WithGenericAltinnMessageTemplate_SkipsContentValidationEvenWhenTextIsMissing(NotificationChannel channel)
    {
        var request = CreateRequest(
            template: NotificationTemplate.GenericAltinnMessage,
            channel: channel,
            emailSubject: null,
            emailBody: null,
            smsBody: null);

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Fact]
    public void Validate_EmailChannel_WithoutEmailContent_ReturnsMissingEmailContent()
    {
        var request = CreateRequest(channel: NotificationChannel.Email, emailSubject: null, emailBody: null);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingEmailContent, error);
    }

    [Fact]
    public void Validate_EmailChannel_WithSubjectButNoBody_ReturnsMissingEmailContent()
    {
        var request = CreateRequest(channel: NotificationChannel.Email, emailSubject: "subject", emailBody: null);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingEmailContent, error);
    }

    [Fact]
    public void Validate_SmsChannel_WithoutSmsContent_ReturnsMissingSmsContent()
    {
        var request = CreateRequest(channel: NotificationChannel.Sms, smsBody: null);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingSmsContent, error);
    }

    [Fact]
    public void Validate_EmailAndSmsChannel_WithOnlyEmailContent_ReturnsMissingEmailAndSmsContent()
    {
        var request = CreateRequest(channel: NotificationChannel.EmailAndSms, smsBody: null);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingEmailAndSmsContent, error);
    }

    [Theory]
    [InlineData(NotificationChannel.EmailPreferred)]
    [InlineData(NotificationChannel.SmsPreferred)]
    public void Validate_PreferredChannel_WithMissingEitherContent_ReturnsMissingPreferredChannel(NotificationChannel channel)
    {
        var request = CreateRequest(channel: channel, smsBody: null);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingPreferredChannel, error);
    }

    [Fact]
    public void Validate_WithReminderMissingContent_ReturnsMissingEmailReminderContent()
    {
        var request = CreateRequest(sendReminder: true, reminderChannel: NotificationChannel.Email);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingEmailReminderContent, error);
    }

    [Fact]
    public void Validate_WithReminderContentProvided_ReturnsNull()
    {
        var request = CreateRequest(
            sendReminder: true,
            reminderChannel: NotificationChannel.Email,
            reminderEmailSubject: "reminder subject",
            reminderEmailBody: "reminder body");

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Fact]
    public void Validate_WithReminderFallingBackToMainChannel_ValidatesUsingMainChannel()
    {
        var request = CreateRequest(sendReminder: true);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.MissingEmailReminderContent, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithoutIdentifier_ReturnsError()
    {
        var request = CreateRequest(customRecipients: [new Recipient()]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithoutIdentifierNotAllowed, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithMultipleIdentifiers_ReturnsError()
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com", MobileNumber = ValidMobileNumber }]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithValidOrganizationNumber_ReturnsNull()
    {
        var request = CreateRequest(customRecipients: [new Recipient { OrganizationNumber = "123456789" }]);

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    [InlineData("has;semicolon@example.com")]
    public void Validate_WithCustomRecipientWithInvalidEmail_ReturnsInvalidEmailProvided(string invalidEmail)
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = invalidEmail }]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.InvalidEmailProvided, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithValidEmail_ReturnsNull()
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("notanumber")]
    [InlineData("+1")]
    public void Validate_WithCustomRecipientWithInvalidMobileNumber_ReturnsInvalidMobileNumberProvided(string invalidMobile)
    {
        var request = CreateRequest(customRecipients: [new Recipient { MobileNumber = invalidMobile }]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.InvalidMobileNumberProvided, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithValidMobileNumber_ReturnsNull()
    {
        var request = CreateRequest(customRecipients: [new Recipient { MobileNumber = ValidMobileNumber }]);

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Fact]
    public void Validate_WithEmailRecipientAndRecipientNameKeyword_ReturnsError()
    {
        var request = CreateRequest(
            emailBody: "Hello $recipientName$",
            customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName, error);
    }

    [Fact]
    public void Validate_WithMobileRecipientAndRecipientNumberKeyword_ReturnsError()
    {
        var request = CreateRequest(
            emailBody: "Your number is $recipientNumber$",
            customRecipients: [new Recipient { MobileNumber = ValidMobileNumber }]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName, error);
    }

    [Fact]
    public void Validate_WithOrganizationRecipientAndRecipientNameKeyword_ReturnsNull()
    {
        var request = CreateRequest(
            emailBody: "Hello $recipientName$",
            customRecipients: [new Recipient { OrganizationNumber = "991825827" }]);

        Assert.Null(NotificationValidationHelper.Validate(request));
    }

    [Fact]
    public void Validate_WithEmailRecipientAndKeywordOnlyInReminderText_ReturnsError()
    {
        var request = CreateRequest(
            sendReminder: true,
            reminderChannel: NotificationChannel.Email,
            reminderEmailSubject: "subject",
            reminderEmailBody: "Hello $recipientName$",
            customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);

        var error = NotificationValidationHelper.Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName, error);
    }
}
