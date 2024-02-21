using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services.Enums;
using Altinn.Broker.Core.Services;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandHandler : IHandler<UploadFileCommandRequest, Guid>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<UploadFileCommandHandler> _logger;

    public UploadFileCommandHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IResourceOwnerRepository resourceOwnerRepository, IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IBrokerStorageService brokerStorageService, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<UploadFileCommandHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _resourceRepository = resourceRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _brokerStorageService = brokerStorageService;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<OneOf<Guid, Error>> Process(UploadFileCommandRequest request)
    {
        var file = await _fileRepository.GetFile(request.FileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, ResourceAccessLevel.Write, request.IsLegacy);
        if (!hasAccess)
        {
            return Errors.FileNotFound;
        };
        if (request.Token.Consumer != file.Sender.ActorExternalId)
        {
            return Errors.FileNotFound;
        }
        if (file.FileStatusEntity.Status > FileStatus.UploadStarted)
        {
            return Errors.FileAlreadyUploaded;
        }
        var resource = await _resourceRepository.GetResource(file.ResourceId);
        if (resource is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(resource.ResourceOwnerId);
        if (resourceOwner?.StorageProvider is null)
        {
            return Errors.ResourceOwnerNotConfigured;
        };

        await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.UploadStarted);
        try
        {
            var checksum = await _brokerStorageService.UploadFile(resourceOwner, file, request.Filestream);
            if (string.IsNullOrWhiteSpace(file.Checksum))
            {
                await _fileRepository.SetChecksum(request.FileId, checksum);
            }
            else if (!string.Equals(checksum, file.Checksum, StringComparison.InvariantCultureIgnoreCase))
            {
                await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.Failed, "Checksum mismatch");
                _backgroundJobClient.Enqueue(() => _brokerStorageService.DeleteFile(resourceOwner, file));
                return Errors.ChecksumMismatch;
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Unexpected error occurred while uploading file: {errorMessage} \nStack trace: {stackTrace}", e.Message, e.StackTrace);
            await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.Failed, "Error occurred while uploading file.");
            await _eventBus.Publish(AltinnEventType.UploadFailed, file.ResourceId, request.FileId.ToString());
            return Errors.UploadFailed;
        }
        await _fileRepository.SetStorageDetails(request.FileId, resourceOwner.StorageProvider.Id, request.FileId.ToString(), request.Filestream.Length);
        await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.UploadProcessing);
        await _eventBus.Publish(AltinnEventType.UploadProcessing, file.ResourceId, request.FileId.ToString());
        if (resourceOwner.StorageProvider.Type == StorageProviderType.Azurite) // When running in Azurite storage emulator, there is no async malwarescan that runs before publish
        {
            await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.Published);
            await _eventBus.Publish(AltinnEventType.Published, file.ResourceId, request.FileId.ToString());
        }
        return file.FileId;
    }
}
