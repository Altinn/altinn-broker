using System.Text.RegularExpressions;

using Altinn.Broker.Application.CreateNotificationOrder;
using Altinn.Broker.Common;
using Altinn.Broker.Common.Constants;
using Altinn.Broker.Core.Models.Notifications;

namespace Altinn.Broker.Application.InitializeFileTransfer;

public static class NotificationValidationHelper
{
    private static readonly Regex EmailRegex = new Regex(@"^((""[^""]+"")|(([a-zA-Z0-9æøåÆØÅ!#$%&'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9æøåÆØÅ!#$%&'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9æøåÆØÅ]([a-zA-Z0-9\-æøåÆØÅ]{0,61})[a-zA-Z0-9æøåÆØÅ]\.)|[a-zA-Z0-9æøåÆØÅ]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))$");

    /// <summary>
    /// "123456789", "0192:123456789" or "urn:altinn:organization:identifier-no:123456789" - no other prefixes.
    /// </summary>
    private static readonly Regex OrganizationNumberRegex = new($@"^(?:0192:|{Regex.Escape(UrnConstants.OrganizationNumberAttribute)}:)?\d{{9}}$");

    /// <summary>
    /// "12345678901" or "urn:altinn:person:identifier-no:12345678901" - no other prefixes. Mod11 is checked separately.
    /// </summary>
    private static readonly Regex NationalIdentityNumberRegex = new($@"^(?:{Regex.Escape(UrnConstants.PersonIdAttribute)}:)?\d{{11}}$");

    public static Error? Validate(NotificationRequest notification, List<string> fileTransferRecipientExternalIds)
    {
        if (notification.CustomRecipients is { Count: > 0 })
        {
            var fileTransferRecipients = fileTransferRecipientExternalIds.Select(id => id.WithoutPrefix()).ToHashSet();
            foreach (var recipient in notification.CustomRecipients)
            {
                var recipientError = ValidateCustomRecipientIdentifier(recipient)
                    ?? ValidateRelatedOrganization(recipient, fileTransferRecipients);
                if (recipientError is not null)
                {
                    return recipientError;
                }
            }
        }

        return null;
    }

    private static Error? ValidateRelatedOrganization(Recipient recipient, HashSet<string> fileTransferRecipients)
    {
        if (string.IsNullOrWhiteSpace(recipient.RelatedOrganizationNumber))
        {
            return NotificationErrors.CustomRecipientWithoutRelatedOrganizationNotAllowed;
        }
        if (!OrganizationNumberRegex.IsMatch(recipient.RelatedOrganizationNumber))
        {
            return NotificationErrors.InvalidOrganizationNumberProvided;
        }
        if (!fileTransferRecipients.Contains(recipient.RelatedOrganizationNumber.WithoutPrefix()))
        {
            return NotificationErrors.CustomRecipientRelatedOrganizationNotARecipient;
        }
        return null;
    }

    private static Error? ValidateCustomRecipientIdentifier(Recipient recipient)
    {
        var identifierCount = new[] { recipient.OrganizationNumber, recipient.EmailAddress, recipient.MobileNumber, recipient.NationalIdentityNumber }
            .Count(identifier => !string.IsNullOrEmpty(identifier));

        if (identifierCount == 0)
        {
            return NotificationErrors.CustomRecipientWithoutIdentifierNotAllowed;
        }
        if (identifierCount > 1)
        {
            return NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed;
        }

        if (recipient.EmailAddress is not null && (!EmailRegex.IsMatch(recipient.EmailAddress) || recipient.EmailAddress.Contains(';')))
        {
            return NotificationErrors.InvalidEmailProvided;
        }
        if (recipient.MobileNumber is not null && !MobileNumberHelper.IsValidMobileNumber(recipient.MobileNumber))
        {
            return NotificationErrors.InvalidMobileNumberProvided;
        }
        if (recipient.OrganizationNumber is not null && !OrganizationNumberRegex.IsMatch(recipient.OrganizationNumber))
        {
            return NotificationErrors.InvalidOrganizationNumberProvided;
        }
        // The regex restricts the prefix to the person URN; IsSocialSecurityNumber strips it and checks mod11.
        if (recipient.NationalIdentityNumber is not null
            && (!NationalIdentityNumberRegex.IsMatch(recipient.NationalIdentityNumber) || !recipient.NationalIdentityNumber.IsSocialSecurityNumber()))
        {
            return NotificationErrors.InvalidNationalIdentityNumberProvided;
        }

        return null;
    }
}
