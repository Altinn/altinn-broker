using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Filters;
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
    /// Requires API key authentication via X-API-Key header.
    /// Rate limiting is enforced per IP address (10 requests per minute).
    /// 
    /// Response includes the following headers:
    /// - X-Report-Total-Records: Number of rows in the parquet file
    /// - X-Report-Total-FileTransfers: Sum of all file transfers across records
    /// - X-Report-Total-ServiceOwners: Count of unique service owners
    /// - X-Report-Generated-At: ISO 8601 timestamp of when the report was generated (UTC)
    /// - X-RateLimit-Limit: Maximum requests allowed per minute
    /// - X-RateLimit-Remaining: Requests remaining in current window
    /// - X-RateLimit-Reset: Unix timestamp when the rate limit resets
    /// - Retry-After: Seconds to wait before retrying (only when rate limited)
    /// </remarks>
    /// <param name="handler">Injected handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Parquet file containing the daily summary statistics with metadata headers</returns>
    /// <response code="200">Report generated successfully, returns Parquet file as octet-stream</response>
    /// <response code="401">Unauthorized - Missing or invalid API key</response>
    /// <response code="403">Forbidden - Invalid API key</response>
    /// <response code="404">No file transfers found</response>
    /// <response code="429">Too Many Requests - Rate limit exceeded</response>
    /// <response code="500">Failed to generate report</response>
    [HttpGet("generate-and-download-daily-summary")]
    [ServiceFilter(typeof(ReportApiKeyFilter))]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateAndDownloadDailySummaryReport(
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(cancellationToken);

        var environment = hostEnvironment.EnvironmentName.ToLowerInvariant();
        var fileName = $"broker_{DateTime.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{environment}.parquet";

        return result.Match<ActionResult>(
            stream =>
            {
                // Add report metadata headers if available
                if (HttpContext.Items.TryGetValue("ReportMetadata", out var metadataObj) 
                    && metadataObj is ReportMetadata metadata)
                {
                    Response.Headers["X-Report-Total-Records"] = metadata.TotalRecords.ToString();
                    Response.Headers["X-Report-Total-FileTransfers"] = metadata.TotalFileTransfers.ToString();
                    Response.Headers["X-Report-Total-ServiceOwners"] = metadata.TotalServiceOwners.ToString();
                    Response.Headers["X-Report-Generated-At"] = metadata.GeneratedAt.ToString("O"); // ISO 8601 format
                }
                
                return File(
                    stream,
                    "application/octet-stream",
                    fileName);
            },
            error => Problem(
                detail: error.Message,
                statusCode: (int)error.StatusCode)
        );
    }
}

