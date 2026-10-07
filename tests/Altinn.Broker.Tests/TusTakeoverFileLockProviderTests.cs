using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusTakeoverFileLockProviderTests
{
    [Fact]
    public async Task Lock_HolderThatDoesNotLetGo_IsAbortedAndTheNewRequestIsRefused()
    {
        var accessor = new HttpContextAccessor();
        var provider = new TusTakeoverFileLockProvider(
            accessor,
            NullLogger<TusTakeoverFileLockProvider>.Instance,
            TimeSpan.FromMilliseconds(200));

        var holderLifetime = new AbortRecordingLifetimeFeature();
        accessor.HttpContext = Request("PATCH", holderLifetime);
        var holderLock = await provider.AquireLock("upload");
        Assert.True(await holderLock.Lock());

        accessor.HttpContext = Request("HEAD", new AbortRecordingLifetimeFeature());
        var newLock = await provider.AquireLock("upload");

        Assert.False(await newLock.Lock());
        Assert.True(holderLifetime.Aborted);
    }

    private static HttpContext Request(string method, IHttpRequestLifetimeFeature lifetime)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Features.Set(lifetime);
        return context;
    }

    private sealed class AbortRecordingLifetimeFeature : IHttpRequestLifetimeFeature
    {
        public bool Aborted { get; private set; }

        public CancellationToken RequestAborted { get; set; }

        public void Abort() => Aborted = true;
    }
}
