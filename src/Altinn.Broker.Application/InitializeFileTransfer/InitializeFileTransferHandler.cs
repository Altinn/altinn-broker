using System.Security.Claims;

using Altinn.Broker.Application.ExpireFileTransfer;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OneOf;

using Serilog.Context;

namespace Altinn.Broker.Application.InitializeFileTransfer;
public class InitializeFileTransferHandler(
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IAuthorizationService resourceRightsRepository,
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IActorFileTransferStatusRepository actorFileTransferStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    IEventBus eventBus,
    IHostEnvironment hostEnvironment,
    ILogger<InitializeFileTransferHandler> logger) : IHandler<InitializeFileTransferRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(InitializeFileTransferRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing file transfer on {resourceId}", request.ResourceId.SanitizeForLogs());
        var hasAccess = await resourceRightsRepository.CheckAccessAsSender(user, request.ResourceId, request.SenderExternalId, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var resource = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        if (request.DisableVirusScan 
            && hostEnvironment.IsProduction() 
            && !resource.ApprovedForDisabledVirusScan)
        {
            return Errors.NotApprovedForDisabledVirusScan;
        }
        var storageProvider = serviceOwner.GetStorageProvider(!request.DisableVirusScan);
        if (storageProvider is null)
        {
            return Errors.StorageProviderNotReady;
        }
        var fileExpirationTime = DateTime.UtcNow.Add(resource.FileTransferTimeToLive ?? TimeSpan.FromDays(30));
        var fileTransferId = await fileTransferRepository.AddFileTransfer(resource, storageProvider, request.FileName, request.SendersFileTransferReference, request.SenderExternalId, request.RecipientExternalIds, fileExpirationTime, request.PropertyList, request.Checksum, !request.DisableVirusScan, cancellationToken);
        LogContext.PushProperty("fileTransferId", fileTransferId);        
        var addRecipientEventTasks = request.RecipientExternalIds.Select(recipientId => actorFileTransferStatusRepository.InsertActorFileTransferStatus(fileTransferId, ActorFileTransferStatus.Initialized, recipientId, cancellationToken));
        try
        {
            await Task.WhenAll(addRecipientEventTasks);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed when adding recipient initialized events: {message}\n{stackTrace}", ex.Message, ex.StackTrace);
            throw;
        }
        var jobId = backgroundJobClient.Schedule<ExpireFileTransferHandler>((ExpireFileTransferHandler) => ExpireFileTransferHandler.Process(new ExpireFileTransferRequest
        {
            FileTransferId = fileTransferId,
            Force = false
        }, null, cancellationToken), fileExpirationTime);
        await fileTransferRepository.SetFileTransferHangfireJobId(fileTransferId, jobId, cancellationToken);
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            await fileTransferStatusRepository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: cancellationToken);
            await eventBus.Publish(AltinnEventType.FileTransferInitialized, resource.Id, fileTransferId.ToString(), request.SenderExternalId, cancellationToken);
            return fileTransferId;
        }, logger, cancellationToken);
    }
}

