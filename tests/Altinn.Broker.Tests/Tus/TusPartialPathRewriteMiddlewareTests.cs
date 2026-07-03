using Altinn.Broker.API.Tus;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusPartialPathRewriteMiddlewareTests
{
    [Fact]
    public void TryRewriteConcatPartialPath_RewritesLegacyTwoSegmentPartialUrl()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/broker/api/v1/filetransfer/upload/tus/5e61f0db-c122-4958-873f-d50f58aa1909/0e3d044db57247aeb7c0a9215c0df9af";

        var rewritten = TusPartialPathRewriteMiddleware.TryRewriteConcatPartialPath(context);

        Assert.True(rewritten);
        Assert.Equal(
            "/broker/api/v1/filetransfer/upload/tus/5e61f0db-c122-4958-873f-d50f58aa1909/partial/0e3d044db57247aeb7c0a9215c0df9af",
            context.Request.Path.Value);
        Assert.Equal("5e61f0db-c122-4958-873f-d50f58aa1909", context.Items[TusRouteHelper.FileTransferIdItemKey]);
    }

    [Fact]
    public void TryRewriteConcatPartialPath_LeavesCanonicalPartialUrlUnchanged()
    {
        var context = new DefaultHttpContext();
        context.Request.Path =
            "/broker/api/v1/filetransfer/upload/tus/5e61f0db-c122-4958-873f-d50f58aa1909/partial/0e3d044db57247aeb7c0a9215c0df9af";

        var rewritten = TusPartialPathRewriteMiddleware.TryRewriteConcatPartialPath(context);

        Assert.False(rewritten);
        Assert.Equal(
            "/broker/api/v1/filetransfer/upload/tus/5e61f0db-c122-4958-873f-d50f58aa1909/partial/0e3d044db57247aeb7c0a9215c0df9af",
            context.Request.Path.Value);
    }

    [Fact]
    public void TryRewriteConcatPartialPath_RewritesLegacyTwoSegmentPartialUrl_WithPathBase()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/broker/api/v1";
        context.Request.Path =
            $"/filetransfer/upload/tus/5e61f0db-c122-4958-873f-d50f58aa1909/0e3d044db57247aeb7c0a9215c0df9af";

        var rewritten = TusPartialPathRewriteMiddleware.TryRewriteConcatPartialPath(context);

        Assert.True(rewritten);
        Assert.Equal(
            "/filetransfer/upload/tus/5e61f0db-c122-4958-873f-d50f58aa1909/partial/0e3d044db57247aeb7c0a9215c0df9af",
            context.Request.Path.Value);
        Assert.Equal("5e61f0db-c122-4958-873f-d50f58aa1909", context.Items[TusRouteHelper.FileTransferIdItemKey]);
    }

    [Fact]
    public void TryRewriteConcatPartialPath_LeavesSingleSegmentUrlUnchanged()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/broker/api/v1/filetransfer/upload/tus/0e3d044db57247aeb7c0a9215c0df9af";

        var rewritten = TusPartialPathRewriteMiddleware.TryRewriteConcatPartialPath(context);

        Assert.False(rewritten);
        Assert.Equal(
            "/broker/api/v1/filetransfer/upload/tus/0e3d044db57247aeb7c0a9215c0df9af",
            context.Request.Path.Value);
    }
}
