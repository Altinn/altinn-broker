namespace Altinn.Broker.API.AltinnPlatformAuth.Options;

/// <summary>
/// End-user session via shared Altinn platform JWT cookie (e.g. on *.altinn.no).
/// </summary>
public class AltinnPlatformAuthSettings
{
    public const string SectionName = "AltinnPlatformAuth";

    /// <summary>
    /// Name of the httpOnly JWT cookie set after platform token refresh.
    /// </summary>
    public string JwtCookieName { get; set; } = "AltinnStudioRuntime";

    /// <summary>
    /// Cookie <c>Domain</c> for shared Altinn hosts (e.g. <c>.altinn.no</c>).
    /// Leave empty for host-only cookies (local dev).
    /// </summary>
    public string CookieDomain { get; set; } = string.Empty;
}
