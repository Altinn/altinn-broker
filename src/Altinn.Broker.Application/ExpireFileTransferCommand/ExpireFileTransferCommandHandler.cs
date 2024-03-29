﻿using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.ExpireFileTransferCommand;
public class ExpireFileTransferCommandHandler : IHandler<ExpireFileTransferCommandRequest, Task>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ExpireFileTransferCommandHandler> _logger;

    public ExpireFileTransferCommandHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceRepository resourceRepository, IEventBus eventBus, ILogger<ExpireFileTransferCommandHandler> logger)
    {
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _resourceRepository = resourceRepository;
        _brokerStorageService = brokerStorageService;
        _eventBus = eventBus;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task<OneOf<Task, Error>> Process(ExpireFileTransferCommandRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting file transfer with id {fileTransferId}", request.FileTransferId.ToString());
        var fileTransfer = await GetFileTransfer(request.FileTransferId, cancellationToken);
        var resource = await GetResource(fileTransfer.ResourceId, cancellationToken);
        var serviceOwner = await GetServiceOwner(resource.ServiceOwnerId);

        if (fileTransfer.FileTransferStatusEntity.Status == Core.Domain.Enums.FileTransferStatus.Deleted)
        {
            _logger.LogInformation("FileTransfer has already been set to deleted");
        }
        else
        {
            await _fileTransferStatusRepository.InsertFileTransferStatus(fileTransfer.FileTransferId, Core.Domain.Enums.FileTransferStatus.Deleted, cancellationToken: cancellationToken);
            await _eventBus.Publish(AltinnEventType.FileDeleted, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), null, cancellationToken);
        }
        if (request.Force || ValidateFileTransferTTL(fileTransfer, serviceOwner))
        {
            await _brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken);
            var recipientsWhoHaveNotDownloaded = fileTransfer.RecipientCurrentStatuses.Where(latestStatus => latestStatus.Status <= Core.Domain.Enums.ActorFileTransferStatus.DownloadConfirmed).ToList();
            foreach (var recipient in recipientsWhoHaveNotDownloaded)
            {
                _logger.LogError("Recipient {recipientExternalReference} did not download the fileTransfer with id {fileTransferId}", recipient.Actor.ActorExternalId, recipient.FileTransferId.ToString());
                await _eventBus.Publish(AltinnEventType.FileNeverConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), recipient.Actor.ActorExternalId, cancellationToken);
            }

        }
        else
        {
            _logger.LogInformation("FileTransfer has not expired, rescheduling expire event");
            await RescheduleExpireEvent(request, cancellationToken);
        }
        return Task.CompletedTask;
    }
    [AutomaticRetry(Attempts = 0)]
    public async Task<OneOf<Task, Error>> RescheduleExpireEvent(ExpireFileTransferCommandRequest request, CancellationToken cancellationToken)
    {
        var fileTransfer = await GetFileTransfer(request.FileTransferId, cancellationToken);
        var resource = await GetResource(fileTransfer.ResourceId, cancellationToken);
        var serviceOwner = await GetServiceOwner(resource.ServiceOwnerId);

        BackgroundJob.Delete(fileTransfer.HangfireJobId);
        if (request.Force)
        {
            BackgroundJob.Enqueue<ExpireFileTransferCommandHandler>((expireFileTransferCommandHandler) => expireFileTransferCommandHandler.Process(new ExpireFileTransferCommandRequest
            {
                FileTransferId = request.FileTransferId,
                Force = true
            }, cancellationToken));
        }
        else
        {
            var newExpireTime = fileTransfer.Created.Add(serviceOwner.FileTransferTimeToLive);
            var hangfireJobId = BackgroundJob.Schedule<ExpireFileTransferCommandHandler>((expireFileTransferCommandHandler) => expireFileTransferCommandHandler.Process(new ExpireFileTransferCommandRequest
            {
                FileTransferId = request.FileTransferId,
                Force = false
            }, cancellationToken), newExpireTime);
            await _fileTransferRepository.SetFileTransferHangfireJobId(request.FileTransferId, hangfireJobId, cancellationToken);
        }
        return Task.CompletedTask;



    }

    private bool ValidateFileTransferTTL(FileTransferEntity fileTransfer, ServiceOwnerEntity serviceOwner)
    {
        var fileTransferTimeToLive = serviceOwner.FileTransferTimeToLive;
        var fileTransferTTL = fileTransferTimeToLive;
        var fileTransferExpires = fileTransfer.Created.Add(fileTransferTTL);
        if (fileTransferExpires < DateTime.UtcNow)
        {
            return true;
        }
        return false;
    }


    private Task<FileTransferEntity> GetFileTransfer(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransfer = _fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            throw new Exception("FileTransfer not found");
        }
        return fileTransfer;
    }
    private Task<ServiceOwnerEntity> GetServiceOwner(string serviceOwnerId)
    {
        var serviceOwner = _serviceOwnerRepository.GetServiceOwner(serviceOwnerId);
        if (serviceOwner is null)
        {
            throw new Exception("ServiceOwner not found");
        }
        return serviceOwner;
    }
    private Task<ResourceEntity> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        var resource = _resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is null)
        {
            throw new Exception("Resource not found");
        }
        return resource;
    }

}
