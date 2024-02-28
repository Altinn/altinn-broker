using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.ExpireFileTransferCommand;
public class ExpireFileTransferCommandHandler : IHandler<Guid, Task>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<ExpireFileTransferCommandHandler> _logger;

    public ExpireFileTransferCommandHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceRepository resourceRepository, ILogger<ExpireFileTransferCommandHandler> logger)
    {
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _resourceRepository = resourceRepository;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<Task, Error>> Process(Guid fileTransferId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting file transfer with id {fileTransferId}", fileTransferId.ToString());
        var fileTransfer = await _fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var service = await _resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (service is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(service.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        if (fileTransfer.FileTransferStatusEntity.Status == Core.Domain.Enums.FileTransferStatus.Deleted)
        {
            _logger.LogInformation("FileTransfer has already been set to deleted");
        }
        else
        {
            await _fileTransferStatusRepository.InsertFileTransferStatus(fileTransferId, Core.Domain.Enums.FileTransferStatus.Deleted, cancellationToken: cancellationToken);
        }
        await _brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken);
        var recipientsWhoHaveNotDownloaded = fileTransfer.RecipientCurrentStatuses.Where(latestStatus => latestStatus.Status <= Core.Domain.Enums.ActorFileTransferStatus.DownloadConfirmed).ToList();
        foreach (var recipient in recipientsWhoHaveNotDownloaded)
        {
            _logger.LogError("Recipient {recipientExternalReference} did not download the fileTransfer with id {fileTransferId}", recipient.Actor.ActorExternalId, recipient.FileTransferId.ToString());
            // TODO, send events
        }
        return Task.CompletedTask;
    }
}
