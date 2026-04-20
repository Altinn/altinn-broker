using System.Security.Claims;
using System.Xml;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.ConfigureResource;
public class ConfigureResourceHandler(IResourceRepository resourceRepository, IAltinnResourceRepository altinnResourceRepository, IServiceOwnerRepository serviceOwnerRepository, IHostEnvironment hostEnvironment, ILogger<ConfigureResourceHandler> logger) : IHandler<ConfigureResourceRequest, Task>
{
    public async Task<OneOf<Task, Error>> Process(ConfigureResourceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing request to configure resource {ResourceId}", request.ResourceId.SanitizeForLogs());

        ResourceEntity? existingResource = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
        ResourceEntity? altinnResourceToCreate = null;

        if (existingResource is null)
        {
            var altinnResource = await altinnResourceRepository.GetResource(request.ResourceId, cancellationToken);
            if (altinnResource is null || string.IsNullOrWhiteSpace(altinnResource.ServiceOwnerId))
            {
                return Errors.InvalidResourceDefinition;
            }
            if (altinnResource.ServiceOwnerId.WithoutPrefix() != user?.GetCallerOrganizationId())
            {
                return Errors.NoAccessToResource;
            }
            if (await serviceOwnerRepository.GetServiceOwner(altinnResource.ServiceOwnerId) is null)
            {
                return Errors.ServiceOwnerHasNotBeenConfigured;
            }
            altinnResourceToCreate = altinnResource;
        }
        else
        {
            if (existingResource.ServiceOwnerId.WithoutPrefix() != user?.GetCallerOrganizationId())
            {
                return Errors.NoAccessToResource;
            }
        }

        var resourceForValidation = existingResource ?? altinnResourceToCreate;

        if (request.MaxFileTransferSize is not null)
        {
            var error = ValidateMaxFileTransferSize(resourceForValidation, request.MaxFileTransferSize.Value);
            if (error is not null) return error;
        }
        if (request.FileTransferTimeToLive is not null)
        {
            var error = ValidateFileTransferTimeToLive(request.FileTransferTimeToLive);
            if (error is not null) return error;
        }
        if (request.PurgeFileTransferGracePeriod is not null)
        {
            var error = ValidatePurgeFileTransferGracePeriod(request.PurgeFileTransferGracePeriod);
            if (error is not null) return error;
        }
        if (request.UseManifestFileShim is not null)
        {
            var error = ValidateUseManifestFileShim(resourceForValidation, request.UseManifestFileShim.Value, request.ExternalServiceCodeLegacy, request.ExternalServiceEditionCodeLegacy);
            if (error is not null) return error;
        }

        if (altinnResourceToCreate is not null)
        {
            existingResource = await resourceRepository.CreateResource(altinnResourceToCreate, cancellationToken);
        }

        if (request.PurgeFileTransferAfterAllRecipientsConfirmed is not null)
        {
            await resourceRepository.UpdatePurgeFileTransferAfterAllRecipientsConfirmed(existingResource!.Id, (bool)request.PurgeFileTransferAfterAllRecipientsConfirmed, cancellationToken);
        }
        if (request.PurgeFileTransferGracePeriod is not null)
        {
            await resourceRepository.UpdatePurgeFileTransferGracePeriod(existingResource!.Id, XmlConvert.ToTimeSpan(request.PurgeFileTransferGracePeriod), cancellationToken);
        }
        if (request.MaxFileTransferSize is not null)
        {
            await resourceRepository.UpdateMaxFileTransferSize(existingResource!.Id, request.MaxFileTransferSize.Value, cancellationToken);
        }
        if (request.FileTransferTimeToLive is not null)
        {
            await resourceRepository.UpdateFileRetention(existingResource!.Id, XmlConvert.ToTimeSpan(request.FileTransferTimeToLive), cancellationToken);
        }
        if (request.ExternalServiceCodeLegacy is not null)
        {
            await resourceRepository.UpdateExternalServiceCodeLegacy(existingResource!.Id, request.ExternalServiceCodeLegacy, cancellationToken);
        }
        if (request.ExternalServiceEditionCodeLegacy is not null)
        {
            await resourceRepository.UpdateExternalServiceEditionCodeLegacy(existingResource!.Id, request.ExternalServiceEditionCodeLegacy.Value, cancellationToken);
        }
        if (request.UseManifestFileShim is not null)
        {
            logger.LogInformation("Updating manifest file shim setting for resource {ResourceId} to {UseManifestFileShim}",
                existingResource!.Id.SanitizeForLogs(), request.UseManifestFileShim.Value);
            await resourceRepository.UpdateUseManifestFileShim(existingResource!.Id, request.UseManifestFileShim.Value, cancellationToken);
        }
        if (request.RequiredParty is not null)
        {
            await resourceRepository.UpdateRequiredParty(existingResource!.Id, request.RequiredParty.Value, cancellationToken);
        }
        return Task.CompletedTask;
    }

    private Error? ValidateMaxFileTransferSize(ResourceEntity? resource, long maxFileTransferSize)
    {
        if (maxFileTransferSize < 0) return Errors.MaxUploadSizeCannotBeNegative;
        if (maxFileTransferSize == 0) return Errors.MaxUploadSizeCannotBeZero;
        if (hostEnvironment.IsProduction()
            && !(resource?.ApprovedForDisabledVirusScan ?? false)
            && maxFileTransferSize > ApplicationConstants.MaxVirusScanUploadSize)
        {
            return Errors.MaxUploadSizeForVirusScan;
        }
        if (maxFileTransferSize > ApplicationConstants.MaxFileUploadSize) return Errors.MaxUploadSizeOverGlobal;
        return null;
    }

    private static Error? ValidateFileTransferTimeToLive(string fileTransferTimeToLiveString)
    {
        TimeSpan fileTransferTimeToLive;
        try
        {
            fileTransferTimeToLive = XmlConvert.ToTimeSpan(fileTransferTimeToLiveString);
        }
        catch (FormatException)
        {
            return Errors.InvalidTimeToLiveFormat;
        }
        if (fileTransferTimeToLive > TimeSpan.FromDays(365)) return Errors.TimeToLiveCannotExceed365Days;
        return null;
    }

    private static Error? ValidatePurgeFileTransferGracePeriod(string purgeFileTransferGracePeriodString)
    {
        TimeSpan purgeFileTransferGracePeriod;
        try
        {
            purgeFileTransferGracePeriod = XmlConvert.ToTimeSpan(purgeFileTransferGracePeriodString);
        }
        catch (FormatException)
        {
            return Errors.InvalidGracePeriodFormat;
        }
        if (purgeFileTransferGracePeriod > XmlConvert.ToTimeSpan(ApplicationConstants.MaxGracePeriod)) return Errors.GracePeriodCannotExceed24Hours;
        return null;
    }

    private static Error? ValidateUseManifestFileShim(ResourceEntity? resource, bool useManifestFileShim, string? externalServiceCode, int? externalServiceCodeEdition)
    {
        if (!useManifestFileShim) return null;
        var actualExternalServiceCode = resource?.ExternalServiceCodeLegacy ?? externalServiceCode;
        var actualExternalServiceCodeEdition = resource?.ExternalServiceEditionCodeLegacy ?? externalServiceCodeEdition;
        if (actualExternalServiceCode is null || actualExternalServiceCodeEdition is null)
        {
            return Errors.NeedServiceCodeForManifestShim;
        }
        return null;
    }
}
