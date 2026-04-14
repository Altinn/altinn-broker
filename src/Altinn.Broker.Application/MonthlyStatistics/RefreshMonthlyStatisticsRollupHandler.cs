using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.MonthlyStatistics;

public class RefreshMonthlyStatisticsRollupHandler(
    IMonthlyStatisticsRepository monthlyStatisticsRepository,
    ILogger<RefreshMonthlyStatisticsRollupHandler> logger)
{
    public async Task RefreshRollup(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await RefreshRollup(now.Year, now.Month, cancellationToken);
    }

    public async Task RefreshPreviousMonthRollup(CancellationToken cancellationToken)
    {
        var previousMonth = DateTime.UtcNow.AddMonths(-1);

        await RefreshRollup(previousMonth.Year, previousMonth.Month, cancellationToken);
    }

    public async Task RefreshRollup(int year, int month, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting monthly statistics rollup refresh for {Year}-{Month}",
            year,
            month);

        await monthlyStatisticsRepository.RebuildMonthlyStatisticsRollupForMonth(
            year,
            month,
            cancellationToken);

        logger.LogInformation(
            "Completed monthly statistics rollup refresh for {Year}-{Month}",
            year,
            month);
    }
}
