using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application.GenerateReport;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

/// <summary>
/// Controller for generating statistical reports on file transfers
/// </summary>
[ApiController]
[Route("broker/api/v1/report")]
public class ReportController(IHostEnvironment hostEnvironment) : Controller
{
    /// <summary>
    /// Generates and downloads a daily summary report as a Parquet file
    /// </summary>
    /// <remarks>
    /// Generates an aggregated report showing the number of file transfers per day, grouped by resource and service owner.
    /// The report is returned directly as a Parquet file for efficient processing and analysis.
    /// </remarks>
    /// <param name="handler">Injected handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Parquet file containing the daily summary statistics</returns>
    /// <response code="200">Report generated successfully, returns Parquet file as octet-stream</response>
    /// <response code="404">No file transfers found</response>
    /// <response code="500">Failed to generate report</response>
    [HttpGet("generate-and-download-daily-summary")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateAndDownloadDailySummaryReport(
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(HttpContext.User, cancellationToken);

        var environment = hostEnvironment.EnvironmentName.ToLowerInvariant();
        var fileName = $"broker_{DateTime.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{environment}.parquet";

        return result.Match<ActionResult>(
            stream => File(
                stream,
                "application/octet-stream",
                fileName),
            error => Problem(
                detail: error.Message,
                statusCode: (int)error.StatusCode)
        );
    }
}

