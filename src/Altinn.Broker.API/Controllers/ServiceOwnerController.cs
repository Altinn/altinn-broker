using System.Net.Http;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Models.ServiceOwner;

using BrokerAuthorizationService = Altinn.Broker.Core.Repositories.IAuthorizationService;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/serviceowner")]
[Authorize(Policy = AuthorizationConstants.ConfigureResource)]
public class ServiceOwnerController(
    IServiceOwnerRepository serviceOwnerRepository,
    IHostEnvironment hostEnvironment,
    IResourceManager resourceManager,
    BrokerAuthorizationService authorizationService,
    IAltinnOrganizationCatalog organizationCatalog) : Controller
{
    /// <summary>
    /// Initializes the service owner for the calling organization within the brokerservice.
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:serviceowner <br/>
    /// Or an end-user session with publish on digdir-broker-administrasjon. End users pass the
    /// organization as <c>onBehalfOf</c>. Poll <see cref="GetServiceOwner"/> for deployment status.
    /// </remarks>
    /// <response code="200">Service owner initialized successfully</response>
    /// <response code="400">Caller organization id could not be determined</response>
    /// <response code="403">End user lacks publish on digdir-broker-administrasjon</response>
    /// <response code="409">Service owner already exists</response>
    /// <response code="500">Service owner could not be initialized</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> InitializeServiceOwner(
        [FromBody] ServiceOwnerInitializeExt serviceOwnerInitializeExt,
        [FromQuery] string? onBehalfOf,
        CancellationToken cancellationToken)
    {
        var serviceOwnerId = await ResolveServiceOwnerId(onBehalfOf, serviceOwnerInitializeExt.OrganizationId, cancellationToken);
        if (serviceOwnerId.Error is not null)
        {
            return serviceOwnerId.Error;
        }

        var existingServiceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId.Id!);
        if (existingServiceOwner is not null)
        {
            return Problem(ServiceOwnerErrors.ServiceOwnerAlreadyExists);
        }

        await serviceOwnerRepository.InitializeServiceOwner(serviceOwnerId.Id!, serviceOwnerInitializeExt.Name);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId.Id!);
        if (serviceOwner is null)
        {
            return Problem(ServiceOwnerErrors.ServiceOwnerInitializationFailed);
        }
        resourceManager.CreateStorageProviders(serviceOwner, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Gets the service owner for the calling organization within the brokerservice.
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br/>
    /// - altinn:serviceowner <br/>
    /// Or an end-user session with publish on digdir-broker-administrasjon and <c>onBehalfOf</c>.
    /// Each storage provider includes its deployment status, which can be polled while setup runs.
    /// </remarks>
    /// <response code="200">Service owner retrieved successfully</response>
    /// <response code="400">Caller organization id could not be determined</response>
    /// <response code="403">End user lacks publish on digdir-broker-administrasjon</response>
    /// <response code="404">Service owner not found</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ServiceOwnerOverviewExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceOwnerOverviewExt>> GetServiceOwner(
        [FromQuery] string? onBehalfOf,
        CancellationToken cancellationToken)
    {
        var serviceOwnerId = await ResolveServiceOwnerId(onBehalfOf, organizationIdOverride: null, cancellationToken);
        if (serviceOwnerId.Error is not null)
        {
            return serviceOwnerId.Error;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId.Id!);
        if (serviceOwner is null)
        {
            return Problem(ServiceOwnerErrors.ServiceOwnerNotFound);
        }

        var deploymentStatuses = new Dictionary<StorageProviderEntity, DeploymentStatus>();
        foreach (var storageProvider in serviceOwner.StorageProviders)
        {
            var deploymentStatus = await resourceManager.GetDeploymentStatus(storageProvider, cancellationToken);
            deploymentStatuses.Add(storageProvider, deploymentStatus);
        }

        return new ServiceOwnerOverviewExt()
        {
            Name = serviceOwner.Name,
            StorageProviders = deploymentStatuses.Zip(serviceOwner.StorageProviders, (status, provider) => new StorageProviderExt()
            {
                Type = (StorageProviderTypeExt)provider.Type,
                DeploymentEnvironment = hostEnvironment.EnvironmentName,
                DeploymentStatus = (DeploymentStatusExt)status.Value
            }).ToList()
        };
    }

    /// <summary>
    /// Service-owner tokens keep using the caller organization. End users act for <paramref name="onBehalfOf"/>
    /// after a publish check on digdir-broker-administrasjon.
    /// </summary>
    private async Task<(string? Id, ActionResult? Error)> ResolveServiceOwnerId(
        string? onBehalfOf,
        string? organizationIdOverride,
        CancellationToken cancellationToken)
    {
        if (HasServiceOwnerScope(User))
        {
            var callerOrganizationId = User.GetCallerOrganizationId();
            if (callerOrganizationId is null)
            {
                return (null, Problem(ServiceOwnerErrors.CallerOrganizationIdNotFound));
            }

            var callerOrganizationIdWithPrefix = callerOrganizationId.WithPrefix();
            if (callerOrganizationIdWithPrefix == "0192:991825827" && !string.IsNullOrWhiteSpace(organizationIdOverride))
            {
                callerOrganizationIdWithPrefix = organizationIdOverride.WithPrefix();
            }

            return (callerOrganizationIdWithPrefix, null);
        }

        if (string.IsNullOrWhiteSpace(onBehalfOf))
        {
            return (null, Problem(Errors.MissingOnBehalfOf));
        }

        var party = onBehalfOf.WithoutPrefix();
        if (!party.IsOrganizationNumber())
        {
            return (null, Problem(Errors.InvalidParty));
        }

        if (!await organizationCatalog.IsPossibleServiceOwner(party, cancellationToken))
        {
            return (null, Problem(Errors.NoPublishAccessToConfigureResource));
        }

        try
        {
            var allowed = await authorizationService.CheckAccessAsPublisher(
                User,
                ApplicationConstants.BrokerBoxConfigureGatekeeperResourceId,
                party,
                cancellationToken);
            if (!allowed)
            {
                return (null, Problem(Errors.NoPublishAccessToConfigureResource));
            }
        }
        catch (HttpRequestException)
        {
            return (null, Problem(Errors.AuthorizationUnavailable));
        }

        return (party.WithPrefix(), null);
    }

    private static bool HasServiceOwnerScope(ClaimsPrincipal user)
    {
        return user.Claims.Any(claim =>
            (claim.Type is "scope" or "scp" or "urn:altinn:scope")
            && claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(AuthorizationConstants.ServiceOwnerScope, StringComparer.Ordinal));
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}

