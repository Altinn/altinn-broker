using System.Globalization;
using System.Text.RegularExpressions;

namespace Altinn.Broker.Common;

public static class StringExtensions
{
    private static readonly Regex OrgPattern = new(@"^(?:\d{9}|0192:\d{9})$");
    private static readonly Regex SsnPattern = new(@"^\d{11}$");
    private static readonly int[] SocialSecurityNumberWeights1 = [3, 7, 6, 1, 8, 9, 4, 5, 2, 1];
    private static readonly int[] SocialSecurityNumberWeights2 = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>
    /// Checks if the provided string is a valid organization number format.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the string matches either a 9-digit format or a '4digits:9digits' format, false otherwise.</returns>
    public static bool IsOrganizationNumber(this string identifier)
    {
        return (!string.IsNullOrWhiteSpace(identifier) && OrgPattern.IsMatch(identifier));
    }

    /// <summary>
    /// Checks if the provided string is a valid social security number format.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the social security number of the identifier matches a 11-digit format and passes mod11 validation.</returns>
    public static bool IsSocialSecurityNumber(this string identifier)
    {
        return IsSocialSecurityNumberWithNoPrefix(identifier.WithoutPrefix());
    }

    /// <summary>
    /// Checks if the provided string is a valid social security number and that it has no prefix.
    /// </summary>
    /// <param name="identifier">The string to validate.</param>
    /// <returns>True if the string matches a 11-digit format and passes mod11 validation.</returns>
    private static bool IsSocialSecurityNumberWithNoPrefix(this string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !SsnPattern.IsMatch(identifier))
        {
            return false;
        }

        // Mod11 validation
        if (identifier.Length < 11)
        {
            return false;
        }

        // Calculate first control digit using Norwegian mod11 standard: k = 11 - (sum % 11)
        int sum1 = 0;
        for (int i = 0; i < 9; i++)
        {
            sum1 += int.Parse(identifier[i].ToString(), CultureInfo.InvariantCulture) * SocialSecurityNumberWeights1[i];
        }
        int control1 = 11 - (sum1 % 11);
        // If result is 11, set to 0. If result is 10, the number is invalid.
        if (control1 == 11)
        {
            control1 = 0;
        }
        else if (control1 == 10)
        {
            return false; // Invalid control digit
        }

        // Calculate second control digit using Norwegian mod11 standard: k = 11 - (sum % 11)
        int sum2 = 0;
        for (int i = 0; i < 10; i++)
        {
            sum2 += int.Parse(identifier[i].ToString(), CultureInfo.InvariantCulture) * SocialSecurityNumberWeights2[i];
        }
        int control2 = 11 - (sum2 % 11);
        // If result is 11, set to 0. If result is 10, the number is invalid.
        if (control2 == 11)
        {
            control2 = 0;
        }
        else if (control2 == 10)
        {
            return false; // Invalid control digit
        }

        return control1 == int.Parse(identifier[9..10], CultureInfo.InvariantCulture) &&
               control2 == int.Parse(identifier[10..11], CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// Extracts the identifier from a colon-separated string that may contain a prefix.
    /// </summary>
    /// <param name="identifierWithPrefix">An identifier with a prefix to format. f.eks an organization number or social security number</param>
    /// <returns>Returns the last sequence succeeding a colon.</returns>
    public static string WithoutPrefix(this string identifierWithPrefix)
    {
        if (string.IsNullOrWhiteSpace(identifierWithPrefix))
        {
            return string.Empty;
        }
        return identifierWithPrefix.Split(":").Last();
    }

    public static string WithPrefix(this string orgNumber)
    {
        if (string.IsNullOrWhiteSpace(orgNumber))
        {
            return string.Empty;
        }
        if (orgNumber.StartsWith("0192:"))
        {
            return orgNumber;
        }
        return $"0192:{orgNumber}";
    }
}
