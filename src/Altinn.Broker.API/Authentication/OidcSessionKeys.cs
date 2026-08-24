namespace Altinn.Broker.API.Authentication;

internal static class OidcSessionKeys
{
    public const string Sid = "id_porten_sid";
    public const string Sub = "id_porten_sub";
    public const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";
}
