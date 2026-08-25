using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.IdPortenDirectAuth.Options;
using Altinn.Broker.Integrations.Altinn;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.IdPortenDirectAuth;

public static class DependencyInjection
{
    /// <summary>
    /// Configuration section name (kept as <c>IdPortenSettings</c> for existing deployments).
    /// </summary>
    public const string ConfigurationSectionName = "IdPortenSettings";

    public static AuthenticationBuilder AddIdPortenDirectAuth(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var services = builder.Services;
        services.Configure<IdPortenDirectAuthSettings>(configuration.GetSection(ConfigurationSectionName));

        var settings = configuration.GetSection(ConfigurationSectionName).Get<IdPortenDirectAuthSettings>()
            ?? new IdPortenDirectAuthSettings();

        services.AddHttpClient<IAltinnTokenExchangeService, AltinnTokenExchangeService>();
        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
        {
            var idPortenSettings = sp.GetRequiredService<IOptions<IdPortenDirectAuthSettings>>().Value;
            var metadataAddress = $"{idPortenSettings.Authority.TrimEnd('/')}/.well-known/openid-configuration";
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });
        });
        services.AddSingleton<IOidcLogoutTokenValidator, OidcLogoutTokenValidator>();
        services.AddSingleton<IOidcBackChannelLogoutSessionStore, OidcBackChannelLogoutSessionStore>();
        services.AddScoped<AltinnTokenCookieEvents>();

        builder
            .AddCookie(AuthorizationConstants.EndUserCookie, options =>
            {
                options.Cookie.Name = settings.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(IdPortenDirectAuthDefaults.CookieLifetimeMinutes);
                options.SlidingExpiration = true;
                options.EventsType = typeof(AltinnTokenCookieEvents);
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = settings.Authority;
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = IdPortenDirectAuthDefaults.CallbackPath;
                options.SignInScheme = AuthorizationConstants.EndUserCookie;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "sub";

                foreach (var scope in settings.Scopes)
                {
                    options.Scope.Add(scope);
                }

                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        if (!string.IsNullOrWhiteSpace(settings.SpaBaseUrl))
                        {
                            context.ProtocolMessage.RedirectUri = settings.OidcCallbackUrl;
                        }

                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        if (!string.IsNullOrWhiteSpace(settings.SpaBaseUrl))
                        {
                            context.ProtocolMessage.PostLogoutRedirectUri =
                                settings.BuildSpaUrl(IdPortenDirectAuthDefaults.PostLogoutRedirectUri);
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var tokenExchange = context.HttpContext.RequestServices.GetRequiredService<IAltinnTokenExchangeService>();
                        var accessToken = context.TokenEndpointResponse?.AccessToken;
                        if (string.IsNullOrEmpty(accessToken))
                        {
                            context.Fail("No access token received from ID-Porten");
                            return;
                        }

                        var requiredAcr = IdPortenDirectAuthDefaults.RequiredAcr;
                        var acr = context.Principal?.FindFirst(ClaimConstants.UserFlow)?.Value
                            ?? context.Principal?.FindFirst("acr")?.Value;
                        if (!string.IsNullOrEmpty(requiredAcr) && acr != requiredAcr)
                        {
                            context.Fail($"Insufficient authentication level. Required: {requiredAcr}, got: {acr}");
                            return;
                        }

                        var altinnToken = await tokenExchange.ExchangeIdPortenToken(accessToken);
                        if (string.IsNullOrEmpty(altinnToken))
                        {
                            context.Fail("Altinn token exchange failed");
                            return;
                        }

                        var sid = context.Principal?.FindFirst("sid")?.Value;
                        var sub = context.Principal?.FindFirst("sub")?.Value;
                        if (!string.IsNullOrEmpty(sid))
                        {
                            context.Properties!.Items[OidcSessionKeys.Sid] = sid;
                        }
                        if (!string.IsNullOrEmpty(sub))
                        {
                            context.Properties!.Items[OidcSessionKeys.Sub] = sub;
                        }

                        var refreshToken = context.TokenEndpointResponse?.RefreshToken ?? string.Empty;
                        context.Properties!.StoreTokens(
                        [
                            new AuthenticationToken { Name = "altinn_token", Value = altinnToken },
                            new AuthenticationToken { Name = "id_porten_refresh_token", Value = refreshToken }
                        ]);

                        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                        var altinnJwt = handler.ReadJwtToken(altinnToken);
                        var identity = new System.Security.Claims.ClaimsIdentity(
                            altinnJwt.Claims,
                            AuthorizationConstants.EndUserCookie,
                            System.Security.Claims.ClaimTypes.Name,
                            System.Security.Claims.ClaimTypes.Role);
                        context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                    }
                };
            });

        return builder;
    }
}
