using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.IdPortenDirectAuth;
using Altinn.Broker.API.Tus;
using Altinn.Broker.Integrations.Altinn;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Broker.Tests;

public class AltinnTokenCookieEventsTests
{
    private const string AltinnTokenName = "altinn_token";
    private const string RefreshTokenName = "id_porten_refresh_token";

    [Fact]
    public async Task ValidatePrincipal_WithLiveAltinnToken_KeepsSessionAndDoesNotRefresh()
    {
        var refreshService = new StubRefreshService();
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(25)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.Equal(0, refreshService.Calls);
    }

    [Fact]
    public async Task ValidatePrincipal_WithExpiredAltinnToken_RefreshesInsteadOfEndingSession()
    {
        var refreshService = new StubRefreshService(new IdPortenTokens("new-access-token", "refresh-2"));
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(-1)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.Equal("refresh-1", refreshService.LastRefreshToken);
        Assert.True(context.ShouldRenew);
        Assert.Equal("refresh-2", context.Properties.GetTokenValue(RefreshTokenName));
        Assert.NotNull(context.Properties.GetTokenValue(AltinnTokenName));
        Assert.Contains(context.Principal!.Claims, claim => claim.Type == "urn:altinn:userid");
    }

    [Fact]
    public async Task ValidatePrincipal_UsesTheRefreshTokenNameStoredAtLogin()
    {
        var refreshService = new StubRefreshService(new IdPortenTokens("new-access-token", "refresh-2"));
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = AltinnTokenName, Value = CreateAltinnToken(TimeSpan.FromMinutes(-1)) }]);
        var context = CreateContext(properties);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(0, refreshService.Calls);
    }

    [Fact]
    public async Task ValidatePrincipal_WithFreshShortLivedAltinnToken_DoesNotRefresh()
    {
        var refreshService = new StubRefreshService(new IdPortenTokens("new-access-token", "refresh-2"));
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromSeconds(120)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.Equal(0, refreshService.Calls);
    }

    [Fact]
    public async Task ValidatePrincipal_RefreshesBeforeTheAltinnTokenExpires()
    {
        var refreshService = new StubRefreshService(new IdPortenTokens("new-access-token", "refresh-2"));
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromSeconds(10)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.Equal(1, refreshService.Calls);
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshFails_RejectsWithoutSigningOut()
    {
        var refreshService = new StubRefreshService(result: null);
        var auth = new TrackingAuthenticationService();
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(-1)),
            refreshToken: "refresh-1",
            authenticationService: auth);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(0, auth.SignOutCalls);
    }

    [Fact]
    public async Task ValidatePrincipal_WhenAltinnTokenExpired_AndActiveTusUpload_AcceptsWithoutRefresh()
    {
        var refreshService = new StubRefreshService(result: null);
        var gracePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("urn:altinn:userid", "1234")],
            AuthorizationConstants.EndUserCookie));
        var tusHelper = new StubTusUploadSessionAuthenticationHelper(gracePrincipal);
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(-1)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService, tusHelper: tusHelper).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.Equal(0, refreshService.Calls);
        Assert.Equal(1, tusHelper.CookieGraceCalls);
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshFails_AndActiveTusUpload_AcceptsGrace()
    {
        // Near-expiry (not fully expired) still tries refresh first; grace is the fallback.
        var refreshService = new StubRefreshService(result: null);
        var gracePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("urn:altinn:userid", "1234")],
            AuthorizationConstants.EndUserCookie));
        var tusHelper = new StubTusUploadSessionAuthenticationHelper(gracePrincipal);
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromSeconds(10)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService, tusHelper: tusHelper).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.Equal(1, refreshService.Calls);
        Assert.Equal(1, tusHelper.CookieGraceCalls);
    }

    [Fact]
    public async Task ValidatePrincipal_WhenAltinnExchangeFails_EndsSession()
    {
        var refreshService = new StubRefreshService(new IdPortenTokens("new-access-token", "refresh-2"));
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(-1)),
            refreshToken: "refresh-1",
            exchangeResult: null);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_LeavesSlidingExpirationToTheCookieMiddleware()
    {
        var refreshService = new StubRefreshService();
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(25)),
            refreshToken: "refresh-1");
        context.ShouldRenew = true;

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.True(context.ShouldRenew);
    }

    [Fact]
    public async Task ValidatePrincipal_WhenSessionRevoked_EndsSession()
    {
        var refreshService = new StubRefreshService();
        var auth = new TrackingAuthenticationService();
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(25)),
            refreshToken: "refresh-1",
            authenticationService: auth);

        await CreateEvents(refreshService, revoked: true).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(0, refreshService.Calls);
        Assert.Equal(1, auth.SignOutCalls);
    }

    [Fact]
    public async Task SigningIn_WhenRefreshTokenWasRotated_AdoptsCachedTokens()
    {
        var refreshService = new StubRefreshService(
            result: null,
            cached: new IdPortenTokens("cached-access", "refresh-2"));
        var originalAltinn = CreateAltinnToken(TimeSpan.FromMinutes(25));
        var context = CreateSigningInContext(originalAltinn, "refresh-1", exchangeResult: "exchanged-altinn-token");

        await CreateEvents(refreshService).SigningIn(context);

        Assert.Equal("refresh-2", context.Properties.GetTokenValue(RefreshTokenName));
        Assert.False(string.IsNullOrEmpty(context.Properties.GetTokenValue(AltinnTokenName)));
        Assert.NotEqual(originalAltinn, context.Properties.GetTokenValue(AltinnTokenName)!);
    }

    [Fact]
    public async Task SigningIn_FollowsSuccessiveCachedRotationHops()
    {
        var refreshService = new StubRefreshService(
            result: null,
            cachedRotations: new Dictionary<string, IdPortenTokens>
            {
                ["refresh-1"] = new("access-2", "refresh-2"),
                ["refresh-2"] = new("access-3", "refresh-3")
            });
        var exchange = new StubTokenExchangeService("exchanged-altinn-token");
        var context = CreateSigningInContext(
            CreateAltinnToken(TimeSpan.FromMinutes(25)),
            "refresh-1",
            tokenExchange: exchange);

        await CreateEvents(refreshService).SigningIn(context);

        Assert.Equal("refresh-3", context.Properties.GetTokenValue(RefreshTokenName));
        Assert.Equal("access-3", exchange.LastAccessToken);
    }

    [Fact]
    public async Task SigningIn_WhenTokenExchangeThrows_KeepsRotatedRefreshToken()
    {
        var refreshService = new StubRefreshService(
            result: null,
            cached: new IdPortenTokens("cached-access", "refresh-2"));
        var originalAltinn = CreateAltinnToken(TimeSpan.FromMinutes(25));
        var context = CreateSigningInContext(
            originalAltinn,
            "refresh-1",
            tokenExchange: new StubTokenExchangeService(exchangeException: new HttpRequestException("exchange down")));

        await CreateEvents(refreshService).SigningIn(context);

        Assert.Equal("refresh-2", context.Properties.GetTokenValue(RefreshTokenName));
        Assert.Equal(originalAltinn, context.Properties.GetTokenValue(AltinnTokenName));
    }

    [Fact]
    public async Task SigningIn_WhenTokenExchangeReturnsNull_KeepsRotatedRefreshToken()
    {
        var refreshService = new StubRefreshService(
            result: null,
            cached: new IdPortenTokens("cached-access", "refresh-2"));
        var originalAltinn = CreateAltinnToken(TimeSpan.FromMinutes(25));
        var context = CreateSigningInContext(
            originalAltinn,
            "refresh-1",
            tokenExchange: new StubTokenExchangeService(result: null));

        await CreateEvents(refreshService).SigningIn(context);

        Assert.Equal("refresh-2", context.Properties.GetTokenValue(RefreshTokenName));
        Assert.Equal(originalAltinn, context.Properties.GetTokenValue(AltinnTokenName));
    }

    [Fact]
    public async Task SigningIn_WhenNoCachedRotation_LeavesCookieTokensUnchanged()
    {
        var refreshService = new StubRefreshService(result: null, cached: null);
        var originalAltinn = CreateAltinnToken(TimeSpan.FromMinutes(25));
        var context = CreateSigningInContext(originalAltinn, "refresh-1");

        await CreateEvents(refreshService).SigningIn(context);

        Assert.Equal("refresh-1", context.Properties.GetTokenValue(RefreshTokenName));
        Assert.Equal(originalAltinn, context.Properties.GetTokenValue(AltinnTokenName));
    }

    private static CookieSigningInContext CreateSigningInContext(
        string altinnToken,
        string refreshToken,
        string? exchangeResult = "exchanged-altinn-token",
        IAltinnTokenExchangeService? tokenExchange = null)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = AltinnTokenName, Value = altinnToken },
            new AuthenticationToken { Name = RefreshTokenName, Value = refreshToken }
        ]);
        var httpContext = CreateHttpContext(exchangeResult, tokenExchange: tokenExchange);
        return new CookieSigningInContext(
            httpContext,
            new AuthenticationScheme(
                AuthorizationConstants.EndUserCookie,
                AuthorizationConstants.EndUserCookie,
                typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            properties,
            new CookieOptions());
    }

    private static AltinnTokenCookieEvents CreateEvents(
        IIdPortenTokenRefreshService refreshService,
        bool revoked = false,
        ITusUploadSessionAuthenticationHelper? tusHelper = null)
        => new(
            new StubLogoutSessionStore(revoked),
            refreshService,
            tusHelper ?? new StubTusUploadSessionAuthenticationHelper());

    private static CookieValidatePrincipalContext CreateContext(
        string altinnToken,
        string refreshToken,
        string? exchangeResult = "exchanged-altinn-token",
        IAuthenticationService? authenticationService = null)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = AltinnTokenName, Value = altinnToken },
            new AuthenticationToken { Name = RefreshTokenName, Value = refreshToken }
        ]);

        return CreateContext(properties, exchangeResult, authenticationService);
    }

    private static CookieValidatePrincipalContext CreateContext(
        AuthenticationProperties properties,
        string? exchangeResult = "exchanged-altinn-token",
        IAuthenticationService? authenticationService = null)
    {
        var httpContext = CreateHttpContext(exchangeResult, authenticationService);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("pid", "idporten-person")],
            IdPortenPrincipalClaimsAuthenticationType));
        var ticket = new AuthenticationTicket(principal, properties, AuthorizationConstants.EndUserCookie);

        return new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(
                AuthorizationConstants.EndUserCookie,
                AuthorizationConstants.EndUserCookie,
                typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            ticket);
    }

    private static DefaultHttpContext CreateHttpContext(
        string? exchangeResult = "exchanged-altinn-token",
        IAuthenticationService? authenticationService = null,
        IAltinnTokenExchangeService? tokenExchange = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAltinnTokenExchangeService>(
            tokenExchange ?? new StubTokenExchangeService(exchangeResult));
        services.AddSingleton<IAuthenticationService>(authenticationService ?? new TrackingAuthenticationService());
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private const string IdPortenPrincipalClaimsAuthenticationType = "IdPorten";

    private static string CreateAltinnToken(TimeSpan validFor)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: "https://platform.tt02.altinn.no/authentication",
            audience: "altinn",
            subject: new ClaimsIdentity([new Claim("urn:altinn:userid", "1234")]),
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.Add(validFor),
            issuedAt: DateTime.UtcNow.AddMinutes(-30));

        return handler.WriteToken(token);
    }

    private sealed class StubRefreshService(
        IdPortenTokens? result = null,
        IdPortenTokens? cached = null,
        IReadOnlyDictionary<string, IdPortenTokens>? cachedRotations = null) : IIdPortenTokenRefreshService
    {
        public int Calls { get; private set; }

        public string? LastRefreshToken { get; private set; }

        public Task<IdPortenTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRefreshToken = refreshToken;
            return Task.FromResult(result);
        }

        public Task<IdPortenTokens?> GetCachedRotationAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            if (cachedRotations is not null)
            {
                return Task.FromResult(
                    cachedRotations.TryGetValue(refreshToken, out var rotated) ? rotated : null);
            }

            return Task.FromResult(cached);
        }
    }

    private sealed class StubTusUploadSessionAuthenticationHelper(ClaimsPrincipal? cookieGracePrincipal = null)
        : ITusUploadSessionAuthenticationHelper
    {
        public int CookieGraceCalls { get; private set; }

        public Task<ClaimsPrincipal?> TryValidateExpiredTokenForActiveUploadAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
            => Task.FromResult<ClaimsPrincipal?>(null);

        public Task<ClaimsPrincipal?> TryAcceptExpiredAltinnCookieForActiveUploadAsync(
            HttpContext httpContext,
            string altinnToken,
            string? sid,
            ClaimsPrincipal? existingPrincipal,
            CancellationToken cancellationToken)
        {
            CookieGraceCalls++;
            return Task.FromResult(cookieGracePrincipal);
        }
    }

    private sealed class StubTokenExchangeService(
        string? result = "exchanged-altinn-token",
        Exception? exchangeException = null) : IAltinnTokenExchangeService
    {
        public string? LastAccessToken { get; private set; }

        public Task<string?> ExchangeIdPortenToken(string idPortenAccessToken, CancellationToken cancellationToken = default)
        {
            LastAccessToken = idPortenAccessToken;
            if (exchangeException is not null)
            {
                throw exchangeException;
            }

            return Task.FromResult(result is null ? null : CreateAltinnToken(TimeSpan.FromMinutes(30)));
        }
    }

    private sealed class StubLogoutSessionStore(bool revoked) : IOidcBackChannelLogoutSessionStore
    {
        public Task<bool> TryConsumeJtiAsync(string jti, TimeSpan timeToLive, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task RevokeAsync(string? sid, string? sub, TimeSpan timeToLive, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> IsRevokedAsync(string? sid, string? sub, DateTimeOffset? cookieIssuedUtc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(revoked);
    }

    private sealed class TrackingAuthenticationService : IAuthenticationService
    {
        public int SignOutCalls { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutCalls++;
            return Task.CompletedTask;
        }
    }
}
