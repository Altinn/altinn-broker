using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

using Altinn.Broker.API.IdPortenDirectAuth.Options;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Validates ID-Porten back-channel logout tokens per
/// OpenID Connect Back-Channel Logout 1.0.
/// </summary>
public sealed class OidcLogoutTokenValidator : IOidcLogoutTokenValidator
{
    private readonly IdPortenDirectAuthSettings _settings;
    private readonly IConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
    private readonly TokenValidationParameters? _testValidationParameters;
    private readonly ILogger<OidcLogoutTokenValidator> _logger;

    public OidcLogoutTokenValidator(
        IOptions<IdPortenDirectAuthSettings> settings,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
        ILogger<OidcLogoutTokenValidator> logger)
    {
        _settings = settings.Value;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public OidcLogoutTokenValidator(
        IdPortenDirectAuthSettings settings,
        TokenValidationParameters testValidationParameters,
        ILogger<OidcLogoutTokenValidator> logger)
    {
        _settings = settings;
        _testValidationParameters = testValidationParameters;
        _logger = logger;
    }

    public async Task<OidcLogoutTokenClaims?> ValidateAsync(string logoutToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logoutToken))
        {
            return null;
        }

        try
        {
            var parameters = await GetValidationParametersAsync(cancellationToken);
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(logoutToken, parameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwt)
            {
                return null;
            }

            if (jwt.Payload.ContainsKey("nonce"))
            {
                _logger.LogWarning("Rejected logout_token because it contains a nonce claim");
                return null;
            }

            if (!HasBackChannelLogoutEvent(jwt))
            {
                _logger.LogWarning("Rejected logout_token because the back-channel logout event is missing");
                return null;
            }

            var sid = jwt.Payload.TryGetValue("sid", out var sidValue) ? sidValue?.ToString() : null;
            var sub = principal.FindFirst("sub")?.Value;
            var jti = jwt.Id;
            if (string.IsNullOrEmpty(jti) || (string.IsNullOrEmpty(sid) && string.IsNullOrEmpty(sub)))
            {
                _logger.LogWarning("Rejected logout_token because jti or sid/sub is missing");
                return null;
            }

            var expiresAt = jwt.ValidTo == DateTime.MinValue
                ? DateTimeOffset.UtcNow.AddMinutes(_settings.CookieLifetimeMinutes)
                : new DateTimeOffset(DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc));

            return new OidcLogoutTokenClaims(sid, sub, jti, expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "logout_token validation failed");
            return null;
        }
    }

    private async Task<TokenValidationParameters> GetValidationParametersAsync(CancellationToken cancellationToken)
    {
        if (_testValidationParameters is not null)
        {
            return _testValidationParameters;
        }

        var configuration = await _configurationManager!.GetConfigurationAsync(cancellationToken);
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.ClientId,
            ValidateLifetime = true,
            RequireExpirationTime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub"
        };
    }

    internal static bool HasBackChannelLogoutEvent(JwtSecurityToken token)
    {
        if (!token.Payload.TryGetValue("events", out var events) || events is null)
        {
            return false;
        }

        const string eventType = OidcSessionKeys.BackChannelLogoutEvent;

        switch (events)
        {
            case JsonElement element when element.ValueKind == JsonValueKind.Object:
                return element.TryGetProperty(eventType, out _);
            case IDictionary<string, object> dictionary:
                return dictionary.ContainsKey(eventType);
            default:
                var json = events.ToString();
                return !string.IsNullOrEmpty(json) && json.Contains(eventType, StringComparison.Ordinal);
        }
    }
}
