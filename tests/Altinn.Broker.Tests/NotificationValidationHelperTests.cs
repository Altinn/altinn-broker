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

    private const string ValidNationalIdentityNumber = "01819012012";
    private const string InvalidNationalIdentityNumber = "01819012013";

    private const string FileTransferRecipient = "123456789";
    private static readonly List<string> FileTransferRecipients = [$"0192:{FileTransferRecipient}"];

    private static NotificationRequest CreateRequest(
        NotificationChannel channel = NotificationChannel.Email,
        bool sendReminder = false,
        List<Recipient>? customRecipients = null,
        bool setRelatedOrganization = true)
    {
        if (setRelatedOrganization)
        {
            customRecipients?.ForEach(recipient => recipient.RelatedOrganizationNumber ??= FileTransferRecipient);
        }
        return new()
        {
            NotificationChannel = channel,
            SendReminder = sendReminder,
            CustomRecipients = customRecipients
        };
    }

    private static Error? Validate(NotificationRequest request) => NotificationValidationHelper.Validate(request, FileTransferRecipients);

    [Fact]
    public void Validate_WithCustomRecipientWithoutRelatedOrganization_ReturnsError()
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }], setRelatedOrganization: false);

        Assert.Equal(NotificationErrors.CustomRecipientWithoutRelatedOrganizationNotAllowed, Validate(request));
    }

    [Fact]
    public void Validate_WithCustomRecipientRelatedToOrganizationThatIsNotARecipient_ReturnsError()
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = "987654321" }]);

        Assert.Equal(NotificationErrors.CustomRecipientRelatedOrganizationNotARecipient, Validate(request));
    }

    [Theory]
    [InlineData(FileTransferRecipient)]
    [InlineData($"0192:{FileTransferRecipient}")]
    [InlineData($"urn:altinn:organization:identifier-no:{FileTransferRecipient}")]
    public void Validate_WithCustomRecipientRelatedToFileTransferRecipient_InAnyAllowedFormat_ReturnsNull(string relatedOrganizationNumber)
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = relatedOrganizationNumber }]);

        Assert.Null(Validate(request));
    }

    [Theory]
    [InlineData(NotificationChannel.Email)]
    [InlineData(NotificationChannel.Sms)]
    [InlineData(NotificationChannel.EmailAndSms)]
    [InlineData(NotificationChannel.EmailPreferred)]
    [InlineData(NotificationChannel.SmsPreferred)]
    public void Validate_WithAnyChannel_ReturnsNull(NotificationChannel channel)
    {
        var request = CreateRequest(channel: channel, sendReminder: true);

        Assert.Null(Validate(request));
    }

    [Fact]
    public void Validate_WithCustomRecipientWithoutIdentifier_ReturnsError()
    {
        var request = CreateRequest(customRecipients: [new Recipient()]);

        var error = Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithoutIdentifierNotAllowed, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithMultipleIdentifiers_ReturnsError()
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com", MobileNumber = ValidMobileNumber }]);

        var error = Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed, error);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("0192:123456789")]
    [InlineData("urn:altinn:organization:identifier-no:123456789")]
    public void Validate_WithCustomRecipientWithValidOrganizationNumber_InAnyAllowedFormat_ReturnsNull(string organizationNumber)
    {
        var request = CreateRequest(customRecipients: [new Recipient { OrganizationNumber = organizationNumber }]);

        Assert.Null(Validate(request));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678a")]
    [InlineData("not-a-number")]
    [InlineData(" ")]
    [InlineData(" 123456789")]
    [InlineData("0192:")]
    [InlineData("0193:123456789")]
    [InlineData("urn:altinn:person:identifier-no:123456789")]
    [InlineData("foo:123456789")]
    public void Validate_WithCustomRecipientWithInvalidOrganizationNumber_ReturnsInvalidOrganizationNumberProvided(string invalidOrganizationNumber)
    {
        var request = CreateRequest(customRecipients: [new Recipient { OrganizationNumber = invalidOrganizationNumber }]);

        Assert.Equal(NotificationErrors.InvalidOrganizationNumberProvided, Validate(request));
    }

    [Theory]
    [InlineData("12345678a")]
    [InlineData("foo:123456789")]
    [InlineData("urn:altinn:person:identifier-no:123456789")]
    public void Validate_WithCustomRecipientWithInvalidRelatedOrganizationNumber_ReturnsInvalidOrganizationNumberProvided(string invalidRelatedOrganizationNumber)
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com", RelatedOrganizationNumber = invalidRelatedOrganizationNumber }]);

        Assert.Equal(NotificationErrors.InvalidOrganizationNumberProvided, Validate(request));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    [InlineData("has;semicolon@example.com")]
    public void Validate_WithCustomRecipientWithInvalidEmail_ReturnsInvalidEmailProvided(string invalidEmail)
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = invalidEmail }]);

        var error = Validate(request);

        Assert.Equal(NotificationErrors.InvalidEmailProvided, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithValidEmail_ReturnsNull()
    {
        var request = CreateRequest(customRecipients: [new Recipient { EmailAddress = "test@example.com" }]);

        Assert.Null(Validate(request));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("notanumber")]
    [InlineData("+1")]
    public void Validate_WithCustomRecipientWithInvalidMobileNumber_ReturnsInvalidMobileNumberProvided(string invalidMobile)
    {
        var request = CreateRequest(customRecipients: [new Recipient { MobileNumber = invalidMobile }]);

        var error = Validate(request);

        Assert.Equal(NotificationErrors.InvalidMobileNumberProvided, error);
    }

    [Theory]
    [InlineData(ValidNationalIdentityNumber)]
    [InlineData($"urn:altinn:person:identifier-no:{ValidNationalIdentityNumber}")]
    public void Validate_WithCustomRecipientWithValidNationalIdentityNumber_WithOrWithoutPersonUrn_ReturnsNull(string nationalIdentityNumber)
    {
        var request = CreateRequest(customRecipients: [new Recipient { NationalIdentityNumber = nationalIdentityNumber }]);

        Assert.Null(Validate(request));
    }

    [Theory]
    [InlineData(InvalidNationalIdentityNumber)]
    [InlineData($"urn:altinn:person:identifier-no:{InvalidNationalIdentityNumber}")]
    [InlineData($"{ValidNationalIdentityNumber}0")]
    [InlineData($" {ValidNationalIdentityNumber}")]
    [InlineData($"urn:altinn:organization:identifier-no:{ValidNationalIdentityNumber}")]
    [InlineData($"0192:{ValidNationalIdentityNumber}")]
    [InlineData($"foo:{ValidNationalIdentityNumber}")]
    [InlineData("urn:altinn:person:identifier-no:")]
    [InlineData("notanumber1")]
    [InlineData(" ")]
    public void Validate_WithCustomRecipientWithInvalidNationalIdentityNumber_ReturnsInvalidNationalIdentityNumberProvided(string invalidNationalIdentityNumber)
    {
        var request = CreateRequest(customRecipients: [new Recipient { NationalIdentityNumber = invalidNationalIdentityNumber }]);

        var error = Validate(request);

        Assert.Equal(NotificationErrors.InvalidNationalIdentityNumberProvided, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithNationalIdentityNumberAndEmail_ReturnsMultipleIdentifiersError()
    {
        var request = CreateRequest(customRecipients: [new Recipient { NationalIdentityNumber = ValidNationalIdentityNumber, EmailAddress = "test@example.com" }]);

        var error = Validate(request);

        Assert.Equal(NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed, error);
    }

    [Fact]
    public void Validate_WithCustomRecipientWithValidMobileNumber_ReturnsNull()
    {
        var request = CreateRequest(customRecipients: [new Recipient { MobileNumber = ValidMobileNumber }]);

        Assert.Null(Validate(request));
    }
}
