namespace Altinn.Broker.Core.Options;

public class AltinnOptions
{
    public string OpenIdWellKnown { get; set; } = string.Empty;
    public string PlatformGatewayUrl { get; set; } = string.Empty;
    public string PlatformSubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Production Altinn platform base URL (for example <c>https://platform.altinn.no</c>).
    /// Set only on TT02 so ConfigureResource can accept tokens issued by production.
    /// </summary>
    public string ProductionPlatformUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL of the TT02 Broker API (for example <c>https://ab.tt02.altinn.no</c>).
    /// Set only in production. When set, the SPA can read and update TT02 resource configuration
    /// with the caller's production Altinn token.
    /// </summary>
    public string StagingBrokerApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// True when <see cref="StagingBrokerApiUrl"/> is an absolute https URL the API can call.
    /// </summary>
    public bool HasStagingBrokerApi()
        => Uri.TryCreate(StagingBrokerApiUrl, UriKind.Absolute, out var stagingUri)
            && stagingUri.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// OpenID metadata for <see cref="ProductionPlatformUrl"/>, or null when that URL is not configured.
    /// </summary>
    public string? ProductionOpenIdWellKnown()
    {
        if (string.IsNullOrWhiteSpace(ProductionPlatformUrl))
        {
            return null;
        }

        return $"{ProductionPlatformUrl.TrimEnd('/')}/authentication/api/v1/openid/.well-known/openid-configuration";
    }
}
