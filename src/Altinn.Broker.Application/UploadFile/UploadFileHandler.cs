using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OneOf;

namespace Altinn.Broker.Application.UploadFile;

public class UploadFileHandler : IHandler<UploadFileRequest, Guid>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<UploadFileHandler> _logger;
    private readonly long _maxFileUploadSize;

    public UploadFileHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IServiceOwnerRepository serviceOwnerRepository, IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IBrokerStorageService brokerStorageService, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<UploadFileHandler> logger, IOptions<ApplicationSettings> applicationSettings)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _resourceRepository = resourceRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileTransferRepository = fileTransferRepository;
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _brokerStorageService = brokerStorageService;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
        _maxFileUploadSize = applicationSettings.Value.MaxFileUploadSize;
    }

    public async Task<OneOf<Guid, Error>> Process(UploadFileRequest request, CancellationToken cancellationToken)
    {
        var fileTransfer = await _fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.FileTransferNotFound;
        };
        if (request.Token.Consumer != fileTransfer.Sender.ActorExternalId)
        {
            return Errors.FileTransferNotFound;
        }
        if (fileTransfer.FileTransferStatusEntity.Status > FileTransferStatus.UploadStarted)
        {
            return Errors.FileTransferAlreadyUploaded;
        }
        var resource = await _resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var maxUploadSize = resource?.MaxFileTransferSize ?? _maxFileUploadSize;
        if (request.ContentLength > maxUploadSize)
        {
            return Errors.FileSizeTooBig;
        }

        await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.UploadStarted, cancellationToken: cancellationToken);
        try
        {
            var checksum = await _brokerStorageService.UploadFile(serviceOwner, fileTransfer, request.UploadStream, cancellationToken);
            if (string.IsNullOrWhiteSpace(fileTransfer.Checksum))
            {
                await _fileTransferRepository.SetChecksum(request.FileTransferId, checksum, cancellationToken);
            }
            else if (!string.Equals(checksum, fileTransfer.Checksum, StringComparison.InvariantCultureIgnoreCase))
            {
                await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Failed, "Checksum mismatch", cancellationToken);
                _backgroundJobClient.Enqueue(() => _brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken));
                return Errors.ChecksumMismatch;
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Unexpected error occurred while uploading file: {errorMessage} \nStack trace: {stackTrace}", e.Message, e.StackTrace);
            await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Failed, "Error occurred while uploading fileTransfer", cancellationToken);
            await _eventBus.Publish(AltinnEventType.UploadFailed, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            return Errors.UploadFailed;
        }
        await _fileTransferRepository.SetStorageDetails(request.FileTransferId, serviceOwner.StorageProvider.Id, request.FileTransferId.ToString(), request.UploadStream.Length, cancellationToken);
        await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: cancellationToken);
        await _eventBus.Publish(AltinnEventType.UploadProcessing, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
        if (serviceOwner.StorageProvider.Type == StorageProviderType.Azurite) // When running in Azurite storage emulator, there is no async malwarescan that runs before publish
        {
            await _fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Published);
            await _eventBus.Publish(AltinnEventType.Published, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
            {
                await _eventBus.Publish(AltinnEventType.Published, fileTransfer.ResourceId, request.FileTransferId.ToString(), recipient.Actor.ActorExternalId, cancellationToken);
            }
        }
        return fileTransfer.FileTransferId;
    }
}
