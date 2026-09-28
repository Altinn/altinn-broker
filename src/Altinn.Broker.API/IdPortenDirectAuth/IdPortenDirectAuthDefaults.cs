namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Fixed ID-Porten direct-auth values — same in every environment.
/// </summary>
internal static class IdPortenDirectAuthDefaults
{
    public const string CallbackPath = "/broker/api/v1/authentication/callback";
    public const string PostLogoutRedirectUri = "/";
    public const string RequiredAcr = "idporten-loa-substantial";

    /// <summary>
    /// Required for ID-Porten to issue a refresh token. Without one the session cannot outlive the
    /// Altinn token, and the user is bounced through login every time it expires.
    /// The ID-Porten client must also be registered with this scope and refresh_token_lifetime > 0.
    /// </summary>
    public const string OfflineAccessScope = "offline_access";
    public const string CookieName = "AltinnBrokerSession";
    public const int CookieLifetimeMinutes = 60;
    public const string BackChannelLogoutPath = "/broker/api/v1/authentication/backchannel-logout";
    public const string FrontChannelLogoutPath = "/broker/api/v1/authentication/frontchannel-logout";

    public static TimeSpan SessionRevocationLifetime =>
        TimeSpan.FromMinutes(CookieLifetimeMinutes + 10);
}
