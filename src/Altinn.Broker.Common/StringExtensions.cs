using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Altinn.Broker.Common.Constants;

namespace Altinn.Broker.Common;

public static class StringExtensions
{
    private static readonly Regex OrgPattern = new(@"^(?:\d{9}|0192:\d{9}|urn:altinn:organization:identifier-no:\d{9})$");
    private static ILogger? _logger;

    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

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
    /// <param name="orgNumber">The organization number to format</param>
    /// <returns>Returns the last sequence succeeding a colon.</returns>
    public static string WithoutPrefix(this string orgNumber)
    {
        if (string.IsNullOrWhiteSpace(orgNumber))
        {
            return string.Empty;
        }
        return orgNumber.Split(":").Last();
    }

    /// <summary>
    /// Formats the organization number with the URN prefix if it doesn't already have one.
    /// </summary>
    /// <param name="orgNumber">The organization number to format</param>
    /// <returns>The organization number with URN prefix</returns>
    public static string WithPrefix(this string orgNumber)
    {
        if (string.IsNullOrWhiteSpace(orgNumber))
        {
            return string.Empty;
        }

        if (orgNumber.StartsWith("0192:"))
        {
            _logger?.LogInformation("Old organization number format (0192:) detected. Converting to URN format: urn:altinn:organization:identifier-no:");
            return $"{UrnConstants.OrganizationNumberAttribute}:{orgNumber.WithoutPrefix()}";
        }

        if (orgNumber.StartsWith(UrnConstants.OrganizationNumberAttribute))
        {
            return orgNumber;
        }

        return $"{UrnConstants.OrganizationNumberAttribute}:{orgNumber}";
    }
}
