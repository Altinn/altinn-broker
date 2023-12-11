using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.DeleteFileCommand;
public class DeleteFileCommandHandler : IHandler<Guid, bool>
{
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;

    public DeleteFileCommandHandler(IFileRepository fileRepository, IFileStatusRepository fileStatusRepository)
    {
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
    }

    public async Task<OneOf<bool, Error>> Process(Guid fileId)
    {
        var file = await _fileRepository.GetFile(fileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }

        var lastStatusForEveryRecipient = file.RecipientCurrentStatuses
            .GroupBy(receipt => receipt.Actor.ActorExternalId)
            .Select(receiptsForRecipient =>
                receiptsForRecipient.MaxBy(receipt => receipt.Date))
            .ToList();
        // Determine if all recipients have downloaded file
        /*foreach (var recipient in file.ActorEvents)
        {
            if (recipient.)
        }*/


        throw new NotImplementedException();
    }
}
