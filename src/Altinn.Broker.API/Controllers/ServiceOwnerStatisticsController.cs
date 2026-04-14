using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.MonthlyStatistics;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/statistics")]
[Authorize(Policy = AuthorizationConstants.ServiceOwner)]
public class ServiceOwnerStatisticsController : Controller
{
    /// <summary>
    /// Export broker statistics for one selected month as CSV for the calling service owner's resources.
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:serviceowner <br/>
    /// 
    /// The CSV contains one row per unique sender and recipient combination for the selected year and month.
    /// </remarks>
    /// <param name="request">Monthly statistics filter parameters</param>
    /// <param name="handler">The handler service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Returns the statistics report as a CSV file download</response>
    /// <response code="400">Invalid year or month values</response>
    /// <response code="401">You must use a bearer token that represents a system user with the altinn:serviceowner scope</response>
    /// <response code="403">The resource needs to be registered as an Altinn 3 resource and it has to be associated with a service owner</response>
    [HttpGet]
    [Route("monthly")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DownloadMonthlyStatisticsCsv(
        [FromQuery] GetMonthlyStatisticsReportRequest request,
        [FromServices] GetMonthlyStatisticsCsvHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(request, HttpContext.User, cancellationToken);

        return result.Match(
            response => File(response.Content, "text/csv", response.FileName),
            Problem);
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
