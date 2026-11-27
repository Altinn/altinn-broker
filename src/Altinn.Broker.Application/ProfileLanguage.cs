using System.Text.RegularExpressions;

namespace Altinn.Broker.Application;

/// <summary>
/// Altinn language codes and the <c>UL</c> segment of the <c>altinnPersistentContext</c> cookie.
/// Bokmål is 1044, English is 1033, and Nynorsk is 2068.
/// </summary>
public static class ProfileLanguage
{
    public const string Bokmal = "nb";
    public const string Nynorsk = "nn";
    public const string English = "en";

    private static readonly Regex UlPattern = new(@"UL=(\d+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> UlByLanguage = new(StringComparer.Ordinal)
    {
        [Bokmal] = "1044",
        [English] = "1033",
        [Nynorsk] = "2068",
    };

    private static readonly Dictionary<string, string> LanguageByUl = new(StringComparer.Ordinal)
    {
        ["1044"] = Bokmal,
        ["1033"] = English,
        ["2068"] = Nynorsk,
    };

    /// <summary>
    /// Accepts <c>nb</c>, <c>nn</c>, <c>en</c>, and the legacy bokmål code <c>no</c>.
    /// </summary>
    public static bool TryNormalize(string? value, out string language)
    {
        var candidate = value?.Trim().ToLowerInvariant();
        if (candidate == "no")
        {
            candidate = Bokmal;
        }

        if (candidate is not null && UlByLanguage.ContainsKey(candidate))
        {
            language = candidate;
            return true;
        }

        language = string.Empty;
        return false;
    }

    public static string? ToUlCode(string language) =>
        UlByLanguage.TryGetValue(language, out var ulCode) ? ulCode : null;

    /// <summary>
    /// Reads <c>UL</c> from a cookie value. The value may already be decoded, or still percent-encoded.
    /// </summary>
    public static string? FromPersistentContext(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var current = raw;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var match = UlPattern.Match(current);
            if (match.Success && LanguageByUl.TryGetValue(match.Groups[1].Value, out var language))
            {
                return language;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(current);
            }
            catch (UriFormatException)
            {
                return null;
            }

            if (decoded == current)
            {
                return null;
            }

            current = decoded;
        }

        return null;
    }

    /// <summary>
    /// Replaces or appends <c>UL</c> and keeps every other segment of the cookie.
    /// </summary>
    public static string MergePersistentContext(string? existing, string ulCode)
    {
        var decoded = existing;
        if (!string.IsNullOrWhiteSpace(existing))
        {
            try
            {
                decoded = Uri.UnescapeDataString(existing);
            }
            catch (UriFormatException)
            {
                decoded = existing;
            }
        }

        if (string.IsNullOrWhiteSpace(decoded))
        {
            return $"UL={ulCode}";
        }

        var parts = decoded.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var replaced = false;
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].StartsWith("UL=", StringComparison.Ordinal))
            {
                parts[index] = $"UL={ulCode}";
                replaced = true;
            }
        }

        if (!replaced)
        {
            return string.Join('&', parts.Append($"UL={ulCode}"));
        }

        return string.Join('&', parts);
    }
}
