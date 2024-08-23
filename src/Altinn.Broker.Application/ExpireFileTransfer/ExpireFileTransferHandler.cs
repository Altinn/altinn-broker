using System.Transactions;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

using Polly;

namespace Altinn.Broker.Application.ExpireFileTransfer;
public class ExpireFileTransferHandler : IHandler<ExpireFileTransferRequest, Task>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ExpireFileTransferHandler> _logger;

    public ExpireFileTransferHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceRepository resourceRepository, IEventBus eventBus, ILogger<ExpireFileTransferHandler> logger)
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
    public async Task<OneOf<Task, Error>> Process(ExpireFileTransferRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting file transfer with id {fileTransferId}", request.FileTransferId.ToString());
        var fileTransfer = await GetFileTransferAsync(request.FileTransferId, cancellationToken);
        var resource = await GetResource(fileTransfer.ResourceId, cancellationToken);
        var serviceOwner = await GetServiceOwnerAsync(resource.ServiceOwnerId);

        if (fileTransfer.FileTransferStatusEntity.Status == Core.Domain.Enums.FileTransferStatus.Purged)
        {
            _logger.LogInformation("FileTransfer has already been set to purged");
        }
        else if (!request.DoNotUpdateStatus)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)) 
            { 
                await _fileTransferStatusRepository.InsertFileTransferStatus(fileTransfer.FileTransferId, Core.Domain.Enums.FileTransferStatus.Purged, cancellationToken: cancellationToken);
                await _eventBus.Publish(AltinnEventType.FilePurged, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            }
        }
        if (request.Force || fileTransfer.ExpirationTime < DateTime.UtcNow)
        {
            await _brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken); // This must be idempotent - i.e not fail on file not existing

            var retryResult = await TransactionPolicy.RetryPolicy(_logger).ExecuteAndCaptureAsync(async () =>
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var recipientsWhoHaveNotDownloaded = fileTransfer.RecipientCurrentStatuses.Where(latestStatus => latestStatus.Status <= Core.Domain.Enums.ActorFileTransferStatus.DownloadConfirmed).ToList();
                    foreach (var recipient in recipientsWhoHaveNotDownloaded)
                    {
                        _logger.LogError("Recipient {recipientExternalReference} did not download the fileTransfer with id {fileTransferId}", recipient.Actor.ActorExternalId, recipient.FileTransferId.ToString());
                        await _eventBus.Publish(AltinnEventType.FileNeverConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), recipient.Actor.ActorExternalId, cancellationToken);
                    }
                    await _eventBus.Publish(AltinnEventType.FileNeverConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
                    transaction.Complete();
                }
            });
            if (retryResult.Outcome == OutcomeType.Failure)
            {
                throw retryResult.FinalException;
            }
        }
        else
        {
            throw new Exception("FileTransfer has not expired, and should not be purged");
        }
        return Task.CompletedTask;
    }
    [AutomaticRetry(Attempts = 0)]

    private async Task<FileTransferEntity> GetFileTransferAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransfer = await _fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            throw new Exception("FileTransfer not found");
        }
        return fileTransfer;
    }
    private async Task<ServiceOwnerEntity> GetServiceOwnerAsync(string serviceOwnerId)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(serviceOwnerId);
        if (serviceOwner is null)
        {
            throw new Exception("ServiceOwner not found");
        }
        return serviceOwner;
    }
    private async Task<ResourceEntity> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is null)
        {
            throw new Exception("Resource not found");
        }
        return resource;
    }

}
