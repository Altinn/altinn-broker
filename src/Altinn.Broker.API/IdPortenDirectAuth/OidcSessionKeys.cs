namespace Altinn.Broker.API.IdPortenDirectAuth;

internal static class OidcSessionKeys
{
    public const string Sid = "id_porten_sid";
    public const string Sub = "id_porten_sub";
    public const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    /// <summary>
    /// Names of the tokens persisted in the auth cookie. These are wire format: renaming one
    /// invalidates every session cookie already issued.
    /// </summary>
    public const string AltinnToken = "altinn_token";
    public const string IdPortenRefreshToken = "id_porten_refresh_token";
}
