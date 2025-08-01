﻿using System.Security.Claims;

using Altinn.Broker.Application.PurgeFileTransfer;
using Altinn.Broker.Application.Middlewares;
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
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IActorFileTransferStatusRepository actorFileTransferStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    IHostEnvironment hostEnvironment,
    ILogger<InitializeFileTransferHandler> logger) : IHandler<InitializeFileTransferRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(InitializeFileTransferRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing file transfer on {resourceId}", request.ResourceId.SanitizeForLogs());
        
        // Log sender format
        if (request.SenderExternalId.StartsWith("urn:altinn:organization:identifier-no:"))
        {
            logger.LogInformation("Sender using new URN format: {sender}", request.SenderExternalId.SanitizeForLogs());
        }
        else if (request.SenderExternalId.StartsWith("0192:"))
        {
            logger.LogInformation("Sender using legacy format: {sender}", request.SenderExternalId.SanitizeForLogs());
        }

        // Log recipients format
        foreach (var recipient in request.RecipientExternalIds)
        {
            if (recipient.StartsWith("urn:altinn:organization:identifier-no:"))
            {
                logger.LogInformation("Recipient using new URN format: {recipient}", recipient.SanitizeForLogs());
            }
            else if (recipient.StartsWith("0192:"))
            {
                logger.LogInformation("Recipient using legacy format: {recipient}", recipient.SanitizeForLogs());
            }
        }

        var hasAccess = await authorizationService.CheckAccessAsSender(user, request.ResourceId, request.SenderExternalId, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var resource = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        }
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
        logger.LogInformation("Filetransfer {fileTransferId} initialized", fileTransferId);
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
        var jobId = backgroundJobClient.Schedule<PurgeFileTransferHandler>((ExpireFileTransferHandler) => ExpireFileTransferHandler.Process(new PurgeFileTransferRequest
        {
            FileTransferId = fileTransferId,
            PurgeTrigger = PurgeTrigger.FileTransferExpiry
        }, null, cancellationToken), fileExpirationTime);
        await fileTransferRepository.SetFileTransferHangfireJobId(fileTransferId, jobId, cancellationToken);
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            await fileTransferStatusRepository.InsertFileTransferStatus(fileTransferId, FileTransferStatus.Initialized, cancellationToken: cancellationToken);
            backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.FileTransferInitialized, resource.Id, fileTransferId.ToString(), request.SenderExternalId, Guid.NewGuid()));
            return fileTransferId;
        }, logger, cancellationToken);
    }
}

