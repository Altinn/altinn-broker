using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusRouteHelperTests
{
    private static readonly Guid FileTransferId = Guid.Parse("b808945c-137d-4e6e-8748-4189b3f79213");
    private static readonly string PartialUploadId = "b4e703b2c01d4dda9297c0021002a907";
    private static readonly string PartialPath =
        $"/broker/api/v1/filetransfer/upload/tus/{FileTransferId}/partial/{PartialUploadId}";

    [Fact]
    public void TryGetFileTransferIdFromRoute_PartialPath_UsesFileTransferIdRouteValue()
    {
        var context = CreateContext(PartialPath);
        context.Request.RouteValues["fileTransferId"] = FileTransferId.ToString();
        context.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey] = PartialUploadId;

        var resolved = TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(FileTransferId, fileTransferId);
    }

    [Fact]
    public void TryGetFileTransferIdFromRoute_PartialPath_DoesNotTreatPartialIdAsFileTransferId()
    {
        var context = CreateContext(PartialPath);
        context.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey] = PartialUploadId;

        var resolved = TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(FileTransferId, fileTransferId);
    }

    [Fact]
    public void TryGetFileTransferIdFromRoute_PartialPath_ParsesFromPathWhenRouteValuesMissing()
    {
        var context = CreateContext(PartialPath);

        var resolved = TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(FileTransferId, fileTransferId);
    }

    [Fact]
    public void TryGetFileTransferIdFromRoute_SingleSegmentUpload_UsesTusFileId()
    {
        var context = CreateContext($"/broker/api/v1/filetransfer/upload/tus/{FileTransferId}");
        context.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey] = FileTransferId.ToString();

        var resolved = TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(FileTransferId, fileTransferId);
    }

    [Theory]
    [InlineData("partial/b4e703b2c01d4dda9297c0021002a907", "b4e703b2c01d4dda9297c0021002a907")]
    [InlineData("b4e703b2c01d4dda9297c0021002a907", "b4e703b2c01d4dda9297c0021002a907")]
    public void NormalizePartialFileId_StripsPartialPrefix(string input, string expected)
    {
        Assert.Equal(expected, TusRouteHelper.NormalizePartialFileId(input));
    }

    [Fact]
    public void TryGetFileTransferIdFromRoute_PartialPath_WithPathBase_ParsesFromPathWhenRouteValuesMissing()
    {
        var context = CreateContext(
            $"/filetransfer/upload/tus/{FileTransferId}/partial/{PartialUploadId}",
            "/broker/api/v1");

        var resolved = TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(FileTransferId, fileTransferId);
    }

    [Fact]
    public void TryParseFileTransferIdFromPartialPath_DoesNotRequireBrokerPrefix()
    {
        var path = $"/filetransfer/upload/tus/{FileTransferId}/partial/{PartialUploadId}";

        var resolved = TusRouteHelper.TryParseFileTransferIdFromPartialPath(path, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(FileTransferId, fileTransferId);
    }

    private static DefaultHttpContext CreateContext(string path, string? pathBase = null)
    {
        var context = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(pathBase))
        {
            context.Request.PathBase = pathBase;
        }

        context.Request.Path = path;
        return context;
    }
}
