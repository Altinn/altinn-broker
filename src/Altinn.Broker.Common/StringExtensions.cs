using System.Text.RegularExpressions;

namespace Altinn.Broker.Common;

public static class StringExtensions
{
    private static readonly Regex OrgPattern = new(@"^(?:\d{9}|0192:\d{9})$");

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
