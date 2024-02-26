using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DeleteFileCommand;
public class DeleteFileCommandHandler : IHandler<Guid, Task>
{
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<DeleteFileCommandHandler> _logger;

    public DeleteFileCommandHandler(IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IResourceOwnerRepository resourceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceRepository resourceRepository, ILogger<DeleteFileCommandHandler> logger)
    {
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceRepository = resourceRepository;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<Task, Error>> Process(Guid fileId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting file with id {fileId}", fileId.ToString());
        var file = await _fileRepository.GetFile(fileId, cancellationToken);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        var service = await _resourceRepository.GetResource(file.ResourceId, cancellationToken);
        if (service is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(service.ResourceOwnerId);
        if (resourceOwner is null)
        {
            return Errors.ResourceOwnerNotConfigured;
        }
        if (file.FileStatusEntity.Status == Core.Domain.Enums.FileStatus.Deleted)
        {
            _logger.LogInformation("File has already been set to deleted");
        }
        else
        {
            await _fileStatusRepository.InsertFileStatus(fileId, Core.Domain.Enums.FileStatus.Deleted, cancellationToken: cancellationToken);
        }
        await _brokerStorageService.DeleteFile(resourceOwner, file, cancellationToken);
        var recipientsWhoHaveNotDownloaded = file.RecipientCurrentStatuses.Where(latestStatus => latestStatus.Status <= Core.Domain.Enums.ActorFileStatus.DownloadConfirmed).ToList();
        foreach (var recipient in recipientsWhoHaveNotDownloaded)
        {
            _logger.LogError("Recipient {recipientExternalReference} did not download the file with id {fileId}", recipient.Actor.ActorExternalId, recipient.FileId.ToString());
            // TODO, send events
        }
        return Task.CompletedTask;
    }
}
