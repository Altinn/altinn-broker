using Altinn.Broker.API.Tus;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusPathBaseNormalizationMiddlewareTests
{
    [Fact]
    public void NormalizeTusPath_MergesPathBaseForPartialUpload()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/broker/api/v1";
        context.Request.Path = "/filetransfer/upload/tus/c8998c3b-534b-487b-9591-f15f22fdcbff/partial/ea724f1786844289befc2ccd737de77c";

        TusPathBaseNormalizationMiddleware.NormalizeTusPath(context);

        Assert.Equal(
            "/broker/api/v1/filetransfer/upload/tus/c8998c3b-534b-487b-9591-f15f22fdcbff/partial/ea724f1786844289befc2ccd737de77c",
            context.Request.Path.Value);
        Assert.Equal(PathString.Empty, context.Request.PathBase);
    }

    [Fact]
    public void NormalizeTusPath_LeavesFullPathUnchanged()
    {
        var context = new DefaultHttpContext();
        context.Request.Path =
            "/broker/api/v1/filetransfer/upload/tus/c8998c3b-534b-487b-9591-f15f22fdcbff/partial/ea724f1786844289befc2ccd737de77c";

        TusPathBaseNormalizationMiddleware.NormalizeTusPath(context);

        Assert.Equal(
            "/broker/api/v1/filetransfer/upload/tus/c8998c3b-534b-487b-9591-f15f22fdcbff/partial/ea724f1786844289befc2ccd737de77c",
            context.Request.Path.Value);
    }
}
