using Altinn.Broker.Application;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileDetailsQuery;
using Altinn.Broker.Application.GetFileOverviewQuery;
using Altinn.Broker.Application.GetFilesQuery;
using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Application.UploadFileCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public LegacyFileController(ILogger<LegacyFileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromServices] InitializeFileCommandHandler handler)
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
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
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
        public async Task<ActionResult<LegacyFileOverviewExt>> GetFileOverview(
            Guid fileId,
            [FromQuery] string onBehalfOfConsumer,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFileOverviewQueryHandler handler)
        {
            CallerIdentity legacyToken = CreateLegacyToken(onBehalfOfConsumer, token);

            LogContextHelpers.EnrichLogsWithToken(legacyToken);
            _logger.LogInformation("Legacy - Getting file overview for {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new GetFileOverviewQueryRequest()
            {
                FileId = fileId,
                Token = legacyToken
            });
            return queryResult.Match(
                result => Ok(LegacyFileStatusOverviewExtMapper.MapToExternalModel(result.File)),
                Problem
            );
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/details")]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
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
            [FromQuery] RecipientFileStatusExt? recipientStatus,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
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
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
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
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] ConfirmDownloadCommandHandler handler)
        {
            throw new NotImplementedException();
        }

        private static CallerIdentity CreateLegacyToken(string onBehalfOfConsumer, CallerIdentity callingToken)
        {
            return new CallerIdentity(callingToken.Scope, onBehalfOfConsumer, callingToken.ClientId);
        }

        private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
