namespace Altinn.Broker.API.Configuration;

public static class AuthorizationConstants
{
    public const string Sender = "Sender";
    public const string Recipient = "Recipient";
    public const string SenderOrRecipient = "SenderOrRecipient";
    public const string Legacy = "Legacy";
    public const string ServiceOwner = "ServiceOwner";

    public const string SenderScope = "altinn:broker.write";
    public const string RecipientScope = "altinn:broker.read";
    public const string LegacyScope = "altinn:broker.legacy";
    public const string ServiceOwnerScope = "altinn:serviceowner";
}
