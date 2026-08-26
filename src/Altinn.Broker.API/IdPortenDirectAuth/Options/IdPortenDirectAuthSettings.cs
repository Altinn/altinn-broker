namespace Altinn.Broker.API.IdPortenDirectAuth.Options;

using Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Configuration for direct ID-Porten OIDC login (Broker as confidential client + session cookie).
/// Bound from the <c>IdPortenSettings</c> configuration section for deployment compatibility.
/// Paths, ACR, and session lifetime are fixed — see <see cref="IdPortenDirectAuthDefaults"/>.
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

    /// <summary>
    /// Public origin of the SPA (e.g. https://localhost:5173 in local Vite).
    /// When set, OIDC callback and post-login redirects use this host so the browser
    /// returns to the frontend (via Vite proxy) instead of the API host.
    /// Leave empty when SPA and API share the same origin.
    /// </summary>
    public string SpaBaseUrl { get; set; } = string.Empty;

    public string CookieName { get; set; } = IdPortenDirectAuthDefaults.CookieName;

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

    public string OidcCallbackUrl => BuildSpaUrl(IdPortenDirectAuthDefaults.CallbackPath);
}
