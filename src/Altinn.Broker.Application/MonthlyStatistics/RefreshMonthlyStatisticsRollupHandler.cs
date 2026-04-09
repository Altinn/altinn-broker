using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.MonthlyStatistics;

public class RefreshMonthlyStatisticsRollupHandler(
    IMonthlyStatisticsRepository monthlyStatisticsRepository,
    ILogger<RefreshMonthlyStatisticsRollupHandler> logger)
{
    public async Task RefreshRollup(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting monthly statistics rollup refresh");
        await monthlyStatisticsRepository.RefreshMonthlyStatisticsRollup(cancellationToken);
        logger.LogInformation("Completed monthly statistics rollup refresh");
    }
}
