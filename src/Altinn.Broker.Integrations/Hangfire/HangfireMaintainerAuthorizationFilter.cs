using System.IdentityModel.Tokens.Jwt;

using Hangfire.Dashboard;

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.Integrations.Hangfire;

public class HangfireMaintainerAuthorizationFilter : IDashboardAuthorizationFilter
{
    private static readonly string HangFireCookieName = "hangfire-dashboard";
    private string _tenantId;
    private string _permittedGroup;
    private ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private TokenValidationParameters _jwtValidationParameters;
    private JwtSecurityTokenHandler _jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

    public HangfireMaintainerAuthorizationFilter(HangfireAuthorizationOptions hangfireAuthOptions)
    {
        _tenantId = hangfireAuthOptions.TenantId;
        _permittedGroup = hangfireAuthOptions.GroupId;
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>($"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
        _jwtValidationParameters = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidAudience = hangfireAuthOptions.Audience,
            ValidIssuer = $"https://sts.windows.net/{_tenantId}/",
            ValidateLifetime = true,
            ConfigurationManager = _configManager
        };
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var accessToken = httpContext.Request.Cookies[HangFireCookieName];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }
        try
        {
            _jwtSecurityTokenHandler.ValidateToken(accessToken, _jwtValidationParameters, out var validatedToken);
            var token = _jwtSecurityTokenHandler.ReadToken(accessToken) as JwtSecurityToken;
            return token?.Claims.Any(claim => claim.Type == "groups" && claim.Value == _permittedGroup) ?? false;
        }
        catch (Exception e)
        {
            return false;
        }
    }
}
