namespace Altinn.Broker.Common;

/// <summary>
/// Maps the acr values ID-porten can return to Altinn authentication levels, so that levels can be
/// compared by strength. ID-porten returns the level the user actually reached, which may be higher
/// than the level the service asked for, so callers must not compare acr values for equality.
/// </summary>
public static class IdPortenAuthenticationLevel
{
    private static readonly Dictionary<string, int> LevelByAuthenticationContext = new(StringComparer.Ordinal)
    {
        ["idporten-loa-high"] = 4,
        ["eidas-loa-high"] = 4,
        ["idporten-loa-substantial"] = 3,
        ["eidas-loa-substantial"] = 3,
        ["idporten-loa-low"] = 2,
        ["selfregistered-email"] = 0
    };

    /// <summary>
    /// Resolves an acr value to an Altinn authentication level.
    /// </summary>
    /// <returns><see langword="false"/> for values we do not recognise, which are never sufficient.</returns>
    public static bool TryGetLevel(string? authenticationContext, out int level)
    {
        if (!string.IsNullOrWhiteSpace(authenticationContext)
            && LevelByAuthenticationContext.TryGetValue(authenticationContext, out level))
        {
            return true;
        }

        level = -1;
        return false;
    }

    /// <summary>
    /// Whether the level the user reached is at least the required one.
    /// </summary>
    public static bool IsSufficient(string? reachedAuthenticationContext, string requiredAuthenticationContext)
        => TryGetLevel(reachedAuthenticationContext, out var reachedLevel)
            && TryGetLevel(requiredAuthenticationContext, out var requiredLevel)
            && reachedLevel >= requiredLevel;
}
