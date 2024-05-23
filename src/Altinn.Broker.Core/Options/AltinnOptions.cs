namespace Altinn.Broker.Core.Options;

public class AltinnOptions
{
    public string OpenIdWellKnown { get; set; } = string.Empty;
    public string PlatformGatewayUrl { get; set; } = string.Empty;
    public string PlatformSubscriptionKey { get; set; } = string.Empty;
}
