﻿namespace Altinn.Broker.Core.Options;

public class AltinnOptions
{
    public string OpenIdWellKnown { get; set; }
    public string LegacyOpenIdWellKnown { get; set; }
    public string PlatformGatewayUrl { get; set; }
    public string PlatformSubscriptionKey { get; set; }
}
