namespace Altinn.Broker.API.IdPortenDirectAuth.Options;

/// <summary>
/// Configuration for direct ID-Porten OIDC login (Broker as confidential client + session cookie).
/// Bound from the <c>IdPortenSettings</c> configuration section for deployment compatibility.
/// </summary>
public class IdPortenDirectAuthSettings
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Must include at least one <c>altinn:*</c> scope. Platform Authentication
    /// rejects ID-Porten exchange when the access token has only openid/profile.
    /// </summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "altinn:portal/enduser"];

    public string CallbackPath { get; set; } = "/broker/api/v1/authentication/callback";
    public string PostLogoutRedirectUri { get; set; } = "/";

    /// <summary>
    /// Public origin of the SPA (e.g. https://localhost:5173 in local Vite).
    /// When set, OIDC callback and post-login redirects use this host so the browser
    /// returns to the frontend (via Vite proxy) instead of the API host.
    /// Leave empty when SPA and API share the same origin.
    /// </summary>
    public string SpaBaseUrl { get; set; } = string.Empty;

    public string RequiredAcr { get; set; } = "idporten-loa-substantial";
    public string CookieName { get; set; } = "AltinnBrokerSession";
    public int CookieLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Path registered as <c>backchannel_logout_uri</c> on the ID-Porten client.
    /// Must be publicly reachable over HTTPS; the domain must match a redirect URI.
    /// </summary>
    public string BackChannelLogoutPath { get; set; } = "/broker/api/v1/authentication/backchannel-logout";

    public TimeSpan SessionRevocationLifetime => TimeSpan.FromMinutes(CookieLifetimeMinutes + 10);

    public string BuildSpaUrl(string? path)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path;
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = "/" + normalizedPath;
        }

        if (string.IsNullOrWhiteSpace(SpaBaseUrl))
        {
            return normalizedPath;
        }

        return $"{SpaBaseUrl.TrimEnd('/')}{normalizedPath}";
    }

    public string OidcCallbackUrl => BuildSpaUrl(CallbackPath);
}
