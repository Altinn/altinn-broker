using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DeleteFileCommand;
public class DeleteFileCommandHandler : IHandler<Guid, bool>
{
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<DeleteFileCommandHandler> _logger;

    public DeleteFileCommandHandler(IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, ILogger<DeleteFileCommandHandler> logger)
    {
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<bool, Error>> Process(Guid fileId)
    {
        var file = await _fileRepository.GetFile(fileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(file.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        if (file.FileStatus >= Core.Domain.Enums.FileStatus.Deleted)
        {
            _logger.LogInformation("File has already been set to deleted");
            return false;
        }

        await _fileStatusRepository.InsertFileStatus(fileId, Core.Domain.Enums.FileStatus.Deleted);
        await _brokerStorageService.DeleteFile(serviceOwner, file);
        var recipientsWhoHaveNotDownloaded = file.RecipientCurrentStatuses.Where(latestStatus => latestStatus.Status <= Core.Domain.Enums.ActorFileStatus.DownloadConfirmed).ToList();
        foreach(var recipient in recipientsWhoHaveNotDownloaded)
        {
            _logger.LogError("Recipient {recipientExternalReference} did not download the file with id {fileId}", recipient.Actor.ActorExternalId, recipient.FileId.ToString());
            // TODO, send events
        }

        return true;
    }
}
