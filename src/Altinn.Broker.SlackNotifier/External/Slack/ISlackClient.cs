namespace Altinn.Broker.SlackNotifier.External.Slack;

internal interface ISlackClient
{
    Task SendAsync(SlackRequestDto message, CancellationToken cancellationToken);
}