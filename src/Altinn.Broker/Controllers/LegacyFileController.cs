using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileDetailsQuery;
using Altinn.Broker.Application.GetFileOverviewQuery;
using Altinn.Broker.Application.GetFilesQuery;
using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Application.UploadFileCommand;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;
using Altinn.Broker.Models.Maskinporten;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Altinn.Broker.Controllers
{
    /// <summary>
    /// The LegacyFileController allows integration from the Altinn 2 BrokerBridge component to allow legacy users access to Altinn 3 Broker
    /// </summary>
    [ApiController]
    [Route("broker/api/legacy/v1/file")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Policy = "Legacy")]
    public class LegacyFileController : Controller
    {
        private readonly ILogger<LegacyFileController> _logger;
        private readonly ProblemDetailsFactory _problemDetailsFactory;

        public LegacyFileController(ILogger<LegacyFileController> logger, ProblemDetailsFactory problemDetailsFactory)
        {
            _logger = logger;
            _problemDetailsFactory = problemDetailsFactory;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token, [FromServices] InitializeFileCommandHandler handler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Upload to an initialized file using a binary stream.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/upload")]
        [Consumes("application/octet-stream")]
        public async Task<ActionResult> UploadFileStreamed(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] UploadFileCommandHandler handler
        )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}")]
        public async Task<ActionResult<FileOverviewExt>> GetFileOverview(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] GetFileOverviewQueryHandler handler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/details")]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] GetFileDetailsQueryHandler handler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get files that can be accessed by the caller according to specified filters.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<List<Guid>>> GetFiles(
            [FromQuery] FileStatusExt? status,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] GetFilesQueryHandler handler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Downloads the file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/download")]
        public async Task<ActionResult> DownloadFile(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] DownloadFileQueryHandler handler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        public async Task<ActionResult> ConfirmDownload(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] ConfirmDownloadCommandHandler handler)
        {
            throw new NotImplementedException();
        }
    }
}
