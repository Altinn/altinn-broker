using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusRouteHelperTests
{
    private const string FileTransferId = "b1919225-0996-47c7-b4de-4c8f4714200e";
    private const string PartialUploadId = "7909c578af754e7f8746a87a1f538e63";

    [Fact]
    public void TryGetFileTransferIdFromPath_CanonicalPartialUrl_ReturnsFileTransferId()
    {
        var path = $"{TusRouteHelper.TusMapPath}/{FileTransferId}/partial/{PartialUploadId}";

        var resolved = TusRouteHelper.TryGetFileTransferIdFromPath(path, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(Guid.Parse(FileTransferId), fileTransferId);
    }

    [Fact]
    public void TryGetFileTransferIdFromRoute_PartialUrl_DoesNotTreatPartialIdAsFileTransferId()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = $"{TusRouteHelper.TusMapPath}/{FileTransferId}/partial/{PartialUploadId}"
            }
        };
        context.Request.RouteValues[TusRouteHelper.TusFileIdRouteKey] = PartialUploadId;

        var resolved = TusRouteHelper.TryGetFileTransferIdFromRoute(context, out var fileTransferId);

        Assert.True(resolved);
        Assert.Equal(Guid.Parse(FileTransferId), fileTransferId);
    }

    [Fact]
    public void TrySetFileTransferIdItemFromPartialPath_SetsItemForCanonicalPartialUrl()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = $"{TusRouteHelper.TusMapPath}/{FileTransferId}/partial/{PartialUploadId}"
            }
        };

        var set = TusRouteHelper.TrySetFileTransferIdItemFromPartialPath(context);

        Assert.True(set);
        Assert.Equal(FileTransferId, context.Items[TusRouteHelper.FileTransferIdItemKey]);
    }

    [Fact]
    public void TryGetPartialUploadIdFromPath_CanonicalPartialUrl_ReturnsPartialUploadId()
    {
        var path = $"{TusRouteHelper.TusMapPath}/{FileTransferId}/partial/{PartialUploadId}";

        var resolved = TusRouteHelper.TryGetPartialUploadIdFromPath(path, out var partialUploadId);

        Assert.True(resolved);
        Assert.Equal(PartialUploadId, partialUploadId);
    }
}
