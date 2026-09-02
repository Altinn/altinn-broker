namespace Altinn.Broker.API.Configuration;

public static class AuthorizationConstants
{
    public const string Sender = "Sender";
    public const string Recipient = "Recipient";
    public const string SenderOrRecipient = "SenderOrRecipient";
    public const string ServiceOwner = "ServiceOwner";
    public const string Maintenance = "Maintenance";
    public const string LegacyAndMaskinporten = "LegacyAndMaskinporten";
    public const string TusUploadSession = "TusUploadSession";
    public const string EndUserCookie = "EndUserCookie";
    /// <summary>Altinn platform JWT stored in the shared runtime httpOnly cookie.</summary>
    public const string AltinnPlatformJwtCookie = "AltinnPlatformJwtCookie";
    public const string EndUser = "EndUser";

    public const string SenderScope = "altinn:broker.write";
    public const string RecipientScope = "altinn:broker.read";
    public const string ServiceOwnerScope = "altinn:serviceowner";
    public const string MaintenanceScope = "altinn:broker.maintenance";
}
