namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Fixed ID-Porten direct-auth values — same in every environment.
/// </summary>
internal static class IdPortenDirectAuthDefaults
{
    public const string CallbackPath = "/broker/api/v1/authentication/callback";
    public const string PostLogoutRedirectUri = "/";
    public const string RequiredAcr = "idporten-loa-substantial";
    public const string CookieName = "AltinnBrokerSession";
    public const int CookieLifetimeMinutes = 60;
    public const string BackChannelLogoutPath = "/broker/api/v1/authentication/backchannel-logout";
    public const string FrontChannelLogoutPath = "/broker/api/v1/authentication/frontchannel-logout";

    public static TimeSpan SessionRevocationLifetime =>
        TimeSpan.FromMinutes(CookieLifetimeMinutes + 10);
}
