using Altinn.Broker.SlackNotifier.Features.AzureAlertToSlackForwarder;

namespace Altinn.Broker.SlackNotifier.External.AppInsights;

internal interface IAppInsightsClient
{
    Task<AppInsightsQueryResponseDto[]> QueryAppInsights(AzureAlertDto azureAlertRequest, CancellationToken cancellationToken);
}

