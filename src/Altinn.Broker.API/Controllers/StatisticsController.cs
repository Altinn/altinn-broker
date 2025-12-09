using Altinn.Broker.API.Filters;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GenerateReport;
using Altinn.Broker.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]  // Hide from public API documentation for now
[Route("broker/api/v1/statistics")]
[ServiceFilter(typeof(StatisticsApiKeyFilter))]
public class StatisticsController(ILogger<StatisticsController> logger) : Controller
{
    private readonly ILogger<StatisticsController> _logger = logger;

    /// <summary>
    /// Generate a daily summary report with aggregated data per service owner per day
    /// </summary>
    /// <remarks>
    /// This generates a parquet file with daily aggregated summary data.
    /// Each row represents one day's usage for one service owner.
    /// Requires API key authentication via X-API-Key header.
    /// Rate limiting is enforced per IP address.
    /// </remarks>
    /// <param name="request">Request parameters</param>
    /// <param name="handler">The handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Returns the summary report generation response</response>
    /// <response code="401">Unauthorized - Missing or invalid API key</response>
    /// <response code="403">Forbidden - Invalid API key</response>
    /// <response code="429">Too Many Requests - Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-daily-summary")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GenerateDailySummaryReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateDailySummary(
        [FromBody] GenerateDailySummaryReportRequest request,
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to generate daily summary report received");

        try
        {
            // Use default request if none provided
            request ??= new GenerateDailySummaryReportRequest();
            
            var result = await handler.Process(request, cancellationToken);
            
            return result.Match(
                Ok,
                Problem
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily summary report");
            return StatusCode(500, "Failed to generate daily summary report");
        }
    }

    /// <summary>
    /// Generate and download a daily summary report with aggregated data per service owner per day
    /// </summary>
    /// <remarks>
    /// This generates a parquet file with daily aggregated summary data and returns it directly as a file download.
    /// Each row represents one day's usage for one service owner.
    /// The response includes both the file and metadata about the report.
    /// Requires API key authentication via X-API-Key header.
    /// Rate limiting is enforced per IP address.
    /// </remarks>
    /// <param name="request">Request parameters</param>
    /// <param name="handler">The handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Returns the parquet file with metadata</response>
    /// <response code="401">Unauthorized - Missing or invalid API key</response>
    /// <response code="403">Forbidden - Invalid API key</response>
    /// <response code="429">Too Many Requests - Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Route("generate-and-download-daily-summary")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerateAndDownloadDailySummary(
        [FromBody] GenerateDailySummaryReportRequest request,
        [FromServices] GenerateDailySummaryReportHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to generate and download daily summary report received");

        try
        {
            // Use default request if none provided
            request ??= new GenerateDailySummaryReportRequest();
            
            var result = await handler.ProcessAndDownload(request, cancellationToken);
            
            return result.Match(
                response => {
                    // Add metadata to response headers
                    Response.Headers["X-File-Hash"] = response.FileHash;
                    Response.Headers["X-File-Size"] = response.FileSizeBytes.ToString();
                    Response.Headers["X-Service-Owner-Count"] = response.ServiceOwnerCount.ToString();
                    Response.Headers["X-Total-FileTransfer-Count"] = response.TotalFileTransferCount.ToString();
                    Response.Headers["X-Generated-At"] = response.GeneratedAt.ToString("O"); // ISO 8601 format
                    Response.Headers["X-Environment"] = response.Environment;
                    Response.Headers["X-Altinn2-Included"] = response.Altinn2Included.ToString();
                    
                    return File(response.FileStream, "application/octet-stream", response.FileName);
                },
                Problem
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and download daily summary report");
            return StatusCode(500, "Failed to generate and download daily summary report");
        }
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}

