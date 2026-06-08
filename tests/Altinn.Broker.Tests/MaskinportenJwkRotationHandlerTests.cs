using Altinn.Broker.Application.MaskinportenJwkRotation;
using Altinn.Broker.Application.SendSlackNotification;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Slack.Webhooks;
using Xunit;

namespace Altinn.Broker.Tests;

public class MaskinportenJwkRotationHandlerTests
{
    [Theory]
    [InlineData(2026, 2, 2, true)]
    [InlineData(2026, 2, 3, false)]
    [InlineData(2026, 3, 2, true)]
    [InlineData(2026, 3, 3, false)]
    [InlineData(2026, 11, 1, false)]
    public void IsFirstWeekdayOfMonth_ReturnsExpectedValue(int year, int month, int day, bool expected)
    {
        var result = MaskinportenJwkRotationHandler.IsFirstWeekdayOfMonth(new DateOnly(year, month, day));

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ProcessScheduled_SkipsRotation_WhenDateIsNotFirstWeekday()
    {
        var rotationService = new Mock<IMaskinportenJwkRotationService>();
        var slackClient = new Mock<ISlackClient>();

        var handler = CreateHandler(
            rotationService.Object,
            slackClient.Object,
            new DateTimeOffset(2026, 2, 3, 8, 0, 0, TimeSpan.Zero));

        await handler.ProcessScheduled(CancellationToken.None);

        rotationService.Verify(service => service.RotateAsync(It.IsAny<CancellationToken>()), Times.Never);
        slackClient.Verify(client => client.PostAsync(It.IsAny<SlackMessage>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScheduled_RunsRotation_WhenDateIsFirstWeekday()
    {
        var rotationService = new Mock<IMaskinportenJwkRotationService>();
        rotationService.Setup(service => service.RotateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaskinportenJwkRotationResult
            {
                Clients =
                [
                    new MaskinportenJwkRotationClientResult
                    {
                        ClientId = "target-client",
                        ClientName = "Broker",
                        NewKid = "kid-1",
                        PreviousKeyCount = 1,
                        CurrentKeyCount = 2,
                        VerificationScope = "scope:a",
                        KeyVaultSecretName = "maskinporten-jwk"
                    }
                ]
            });

        var slackClient = new Mock<ISlackClient>();

        var handler = CreateHandler(
            rotationService.Object,
            slackClient.Object,
            new DateTimeOffset(2026, 2, 2, 8, 0, 0, TimeSpan.Zero));

        await handler.ProcessScheduled(CancellationToken.None);

        rotationService.Verify(service => service.RotateAsync(It.IsAny<CancellationToken>()), Times.Once);
        slackClient.Verify(client => client.PostAsync(It.Is<SlackMessage>(message =>
            message.Text.StartsWith(":white_check_mark: *Maskinporten JWK rotation completed*")
            && message.Text.Contains("*System:* Broker"))), Times.Once);
    }

    private static MaskinportenJwkRotationHandler CreateHandler(
        IMaskinportenJwkRotationService rotationService,
        ISlackClient slackClient,
        DateTimeOffset utcNow)
    {
        var slackHandler = new SendSlackNotificationHandler(
            slackClient,
            new SlackSettings(new FakeHostEnvironment()),
            new FakeHostEnvironment(),
            NullLogger<SendSlackNotificationHandler>.Instance);

        return new MaskinportenJwkRotationHandler(
            rotationService,
            slackHandler,
            new FixedTimeProvider(utcNow),
            NullLogger<MaskinportenJwkRotationHandler>.Instance);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Altinn.Broker.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
