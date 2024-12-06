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
    /// <param name="orgOrSsnNumber">The organization number or social security number to format</param>
    /// <returns>Returns the last sequence succeeding a colon.</returns>
    public static string WithoutPrefix(this string orgOrSsnNumber)
    {
        if (string.IsNullOrWhiteSpace(orgOrSsnNumber))
        {
            return string.Empty;
        }
        return orgOrSsnNumber.Split(":").Last();
    }
}
