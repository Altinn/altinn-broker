using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.IdPortenDirectAuth;
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
    public async Task ValidatePrincipal_WhenRefreshFails_EndsSession()
    {
        var refreshService = new StubRefreshService(result: null);
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(-1)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Null(context.Principal);
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
        var context = CreateContext(
            altinnToken: CreateAltinnToken(TimeSpan.FromMinutes(25)),
            refreshToken: "refresh-1");

        await CreateEvents(refreshService, revoked: true).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(0, refreshService.Calls);
    }

    private static AltinnTokenCookieEvents CreateEvents(IIdPortenTokenRefreshService refreshService, bool revoked = false)
        => new(new StubLogoutSessionStore(revoked), refreshService);

    private static CookieValidatePrincipalContext CreateContext(
        string altinnToken,
        string refreshToken,
        string? exchangeResult = "exchanged-altinn-token")
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = AltinnTokenName, Value = altinnToken },
            new AuthenticationToken { Name = RefreshTokenName, Value = refreshToken }
        ]);

        return CreateContext(properties, exchangeResult);
    }

    private static CookieValidatePrincipalContext CreateContext(
        AuthenticationProperties properties,
        string? exchangeResult = "exchanged-altinn-token")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAltinnTokenExchangeService>(new StubTokenExchangeService(exchangeResult));
        services.AddSingleton<IAuthenticationService>(new NoOpAuthenticationService());

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
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

    private sealed class StubRefreshService(IdPortenTokens? result = null) : IIdPortenTokenRefreshService
    {
        public int Calls { get; private set; }

        public string? LastRefreshToken { get; private set; }

        public Task<IdPortenTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRefreshToken = refreshToken;
            return Task.FromResult(result);
        }
    }

    private sealed class StubTokenExchangeService(string? result) : IAltinnTokenExchangeService
    {
        public Task<string?> ExchangeIdPortenToken(string idPortenAccessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(result is null ? null : CreateAltinnToken(TimeSpan.FromMinutes(30)));
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

    private sealed class NoOpAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
