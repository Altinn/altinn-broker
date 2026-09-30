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

        if (mobileNumber.StartsWith("00"))
        {
            mobileNumber = "+" + mobileNumber.Remove(0, 2);
        }

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
}