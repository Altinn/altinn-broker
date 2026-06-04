using Altinn.Broker.API.Controllers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Altinn.Broker.Tests;

public class MaintenanceControllerMaskinportenJwkRotationTests
{
    [Fact]
    public void TriggerMaskinportenJwkRotation_ReturnsBadRequest_WhenConfirmationIsWrong()
    {
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var controller = CreateController();

        var result = controller.TriggerMaskinportenJwkRotation(
            backgroundJobClient.Object,
            new TriggerMaskinportenJwkRotationRequest { Confirmation = "dry-run" });

        Assert.IsType<BadRequestObjectResult>(result);
        backgroundJobClient.Verify(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public void TriggerMaskinportenJwkRotation_EnqueuesJob_WhenConfirmationIsCorrect()
    {
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        backgroundJobClient
            .Setup(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-1");
        var controller = CreateController();

        var result = controller.TriggerMaskinportenJwkRotation(
            backgroundJobClient.Object,
            new TriggerMaskinportenJwkRotationRequest { Confirmation = "rotate-maskinporten-jwk" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        backgroundJobClient.Verify(client => client.Create(
            It.Is<Job>(job => job.Method.Name == "Process"),
            It.IsAny<IState>()), Times.Once);
    }

    private static MaintenanceController CreateController()
        => new(NullLogger<MaintenanceController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
}
