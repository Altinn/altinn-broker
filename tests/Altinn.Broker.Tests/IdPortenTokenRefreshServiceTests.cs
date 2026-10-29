using System.Net;
using System.Text;

using Altinn.Broker.API.IdPortenDirectAuth;
using Altinn.Broker.API.IdPortenDirectAuth.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Xunit;

namespace Altinn.Broker.Tests;

public class IdPortenTokenRefreshServiceTests
{
    private const string TokenEndpoint = "https://test.idporten.no/token";

    [Fact]
    public async Task RefreshAsync_ReturnsRotatedTokens()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"access_token":"access-2","refresh_token":"refresh-2"}"""));
        var service = CreateService(handler);

        var tokens = await service.RefreshAsync("refresh-1");

        Assert.NotNull(tokens);
        Assert.Equal("access-2", tokens!.AccessToken);
        Assert.Equal("refresh-2", tokens.RefreshToken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RefreshAsync_SendsRefreshTokenGrantWithClientCredentials()
    {
        string? body = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"access_token":"access-2","refresh_token":"refresh-2"}""");
        });

        await CreateService(handler).RefreshAsync("refresh-1");

        Assert.Contains("grant_type=refresh_token", body);
        Assert.Contains("refresh_token=refresh-1", body);
        Assert.Contains("client_id=broker-test-client", body);
        Assert.Contains("client_secret=secret", body);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCallsWithSameToken_RedeemItOnce()
    {
        var gate = new TaskCompletionSource();
        var handler = new StubHttpMessageHandler(_ =>
        {
            gate.Task.GetAwaiter().GetResult();
            return Json("""{"access_token":"access-2","refresh_token":"refresh-2"}""");
        });
        var service = CreateService(handler);

        var first = Task.Run(() => service.RefreshAsync("refresh-1"));
        var second = Task.Run(() => service.RefreshAsync("refresh-1"));
        await Task.Delay(50);
        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, handler.Calls);
        Assert.All(results, tokens => Assert.Equal("refresh-2", tokens!.RefreshToken));
    }

    [Fact]
    public async Task RefreshAsync_SameTokenTwice_ReplaysCachedResult()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"access_token":"access-2","refresh_token":"refresh-2"}"""));
        var service = CreateService(handler);

        await service.RefreshAsync("refresh-1");
        var replayed = await service.RefreshAsync("refresh-1");

        Assert.Equal(1, handler.Calls);
        Assert.Equal("access-2", replayed!.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_WhenIdPortenRejectsTheGrant_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest));

        Assert.Null(await CreateService(handler).RefreshAsync("refresh-1"));
    }

    [Fact]
    public async Task RefreshAsync_AcrossInstances_WhenLoserGetsInvalidGrant_ReplaysWinnerCache()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var winnerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loserMayFail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;

        var handler = new StubHttpMessageHandler(_ =>
        {
            if (Interlocked.Increment(ref call) == 1)
            {
                winnerStarted.SetResult();
                loserMayFail.Task.GetAwaiter().GetResult();
                return Json("""{"access_token":"access-2","refresh_token":"refresh-2"}""");
            }

            return Json("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest);
        });

        var winner = CreateService(handler, cache, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(20));
        var loser = CreateService(handler, cache, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(20));

        var winnerTask = Task.Run(() => winner.RefreshAsync("refresh-1"));
        await winnerStarted.Task;
        var loserTask = Task.Run(() => loser.RefreshAsync("refresh-1"));
        await Task.Delay(50);
        loserMayFail.SetResult();

        var results = await Task.WhenAll(winnerTask, loserTask);

        Assert.Equal(2, handler.Calls);
        Assert.All(results, tokens =>
        {
            Assert.NotNull(tokens);
            Assert.Equal("access-2", tokens!.AccessToken);
            Assert.Equal("refresh-2", tokens.RefreshToken);
        });
    }

    [Fact]
    public async Task RefreshAsync_WhenIdPortenIsUnreachable_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));

        Assert.Null(await CreateService(handler).RefreshAsync("refresh-1"));
    }

    [Fact]
    public async Task RefreshAsync_WhenTheCallTimesOut_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("timeout"));

        Assert.Null(await CreateService(handler).RefreshAsync("refresh-1"));
    }

    [Fact]
    public async Task RefreshAsync_WhenTheCallerCancels_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(handler).RefreshAsync("refresh-1", cancellation.Token));
    }

    [Fact]
    public async Task RefreshAsync_WithoutRefreshToken_ReturnsNullWithoutCallingIdPorten()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"access_token":"access-2"}"""));

        Assert.Null(await CreateService(handler).RefreshAsync(string.Empty));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task RefreshAsync_WhenOidcMetadataFailsOnce_RetriesRedeemSuccessfully()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"access_token":"access-2","refresh_token":"refresh-2"}"""));
        var configuration = new FlakyConfigurationManager(failuresBeforeSuccess: 1);

        var tokens = await CreateService(handler, configurationManager: configuration).RefreshAsync("refresh-1");

        Assert.NotNull(tokens);
        Assert.Equal("refresh-2", tokens!.RefreshToken);
        Assert.Equal(2, configuration.Calls);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RefreshAsync_WhenOidcMetadataKeepsFailing_DoesNotCallIdPorten()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"access_token":"access-2","refresh_token":"refresh-2"}"""));
        var configuration = new FlakyConfigurationManager(failuresBeforeSuccess: int.MaxValue);

        Assert.Null(await CreateService(handler, configurationManager: configuration).RefreshAsync("refresh-1"));
        Assert.Equal(2, configuration.Calls);
        Assert.Equal(0, handler.Calls);
    }

    private static IdPortenTokenRefreshService CreateService(
        StubHttpMessageHandler handler,
        IDistributedCache? cache = null,
        TimeSpan? concurrentRefreshWait = null,
        TimeSpan? concurrentRefreshPollInterval = null,
        IConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null)
    {
        var settings = new IdPortenDirectAuthSettings
        {
            Authority = "https://test.idporten.no",
            ClientId = "broker-test-client",
            ClientSecret = "secret"
        };

        return new IdPortenTokenRefreshService(
            new StubHttpClientFactory(handler),
            Options.Create(settings),
            configurationManager ?? new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration { TokenEndpoint = TokenEndpoint }),
            cache ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<IdPortenTokenRefreshService>.Instance,
            concurrentRefreshWait ?? TimeSpan.Zero,
            concurrentRefreshPollInterval ?? TimeSpan.Zero,
            redis: null);
    }

    [Fact]
    public async Task GetCachedRotationAsync_AfterRefresh_ReturnsRotatedTokens()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"access_token":"access-2","refresh_token":"refresh-2"}"""));
        var service = CreateService(handler);

        await service.RefreshAsync("refresh-1");
        var cached = await service.GetCachedRotationAsync("refresh-1");

        Assert.NotNull(cached);
        Assert.Equal("refresh-2", cached!.RefreshToken);
        Assert.Null(await service.GetCachedRotationAsync("refresh-unknown"));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(handler(request));
        }
    }

    private sealed class FlakyConfigurationManager(int failuresBeforeSuccess)
        : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public int Calls { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            Calls++;
            if (Calls <= failuresBeforeSuccess)
            {
                throw new InvalidOperationException("OIDC metadata unavailable");
            }

            return Task.FromResult(new OpenIdConnectConfiguration { TokenEndpoint = TokenEndpoint });
        }

        public void RequestRefresh()
        {
        }
    }
}
