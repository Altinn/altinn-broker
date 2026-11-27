using PhoneNumbers;

namespace Altinn.Broker.Application.CreateNotificationOrder;

/// <summary>
/// Validates mobile numbers for notification recipients.
/// </summary>
public static class MobileNumberHelper
{

    /// <summary>
    /// Validated as mobile number based on the Altinn 2 regex
    /// </summary>
    /// <param name="mobileNumber">The string to validate as an mobile number</param>
    /// <returns>A boolean indicating that the mobile number is valid or not</returns>
    public static bool IsValidMobileNumber(string? mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || (!mobileNumber.StartsWith('+') && !mobileNumber.StartsWith("00")))
        {
            return false;
        }

        mobileNumber = Normalize(mobileNumber);

        var phoneNumberUtil = PhoneNumberUtil.GetInstance();

        try
        {
            var phoneNumber = phoneNumberUtil.Parse(mobileNumber, null);
            return phoneNumberUtil.IsValidNumber(phoneNumber);
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <summary>
    /// The one form a mobile number is stored and sent in, so the same number written differently - "0047…", "+47…",
    /// with spaces, hyphens, dots or parentheses - is the same recipient: E.164, "+" followed by digits only.
    /// </summary>
    public static string Normalize(string mobileNumber)
    {
        var compact = string.Concat(mobileNumber.Where(c => !char.IsWhiteSpace(c)));
        var international = compact.StartsWith("00") ? "+" + compact[2..] : compact;

        var phoneNumberUtil = PhoneNumberUtil.GetInstance();
        try
        {
            return phoneNumberUtil.Format(phoneNumberUtil.Parse(international, null), PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            // Not reached for a validated number - validation rejects what does not parse.
            return international;
        }
    }
}