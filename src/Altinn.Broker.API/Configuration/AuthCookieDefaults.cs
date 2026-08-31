namespace Altinn.Broker.API.Configuration;

/// <summary>
/// Shared cookie policy for end-user auth cookies (IdPorten session and Altinn platform JWT).
/// SameSite=None + Secure is required when the SPA and API sit on different site contexts
/// (e.g. Front Door host calling APIM, or cross-subdomain *.altinn.no).
/// </summary>
public static class AuthCookieDefaults
{
    public const SameSiteMode SameSite = SameSiteMode.None;
}
