using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Tests.Helpers;

namespace Altinn.Broker.Tests.Factories;
internal static class FileTransferEntityFactory
{
    internal static FileTransferEntity BasicFileTransfer()
    {
        var fileTransferId = Guid.NewGuid();
        return new()
        {
            FileTransferId = fileTransferId,
            ResourceId = TestConstants.RESOURCE_FOR_TEST,
            Checksum = null,
            FileName = "input.txt",
            PropertyList = [],
            RecipientCurrentStatuses = new List<ActorFileTransferStatusEntity>
            {
                new ActorFileTransferStatusEntity
                {
                    Actor = new ActorEntity()
                    {
                        ActorExternalId = "0192:986252932"
                    },
                    Date = DateTime.UtcNow,
                    FileTransferId = fileTransferId
                }
            },
            Sender = new ActorEntity()
            {
                ActorExternalId = "0192:991825827"
            },
            SendersFileTransferReference = "test-data",
            Created = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddHours(1),
            FileTransferStatusEntity = new FileTransferStatusEntity()
            {
                FileTransferId = fileTransferId,
                Date = DateTime.UtcNow,
                DetailedStatus = "Ready for download",
                Status = FileTransferStatus.Published
            }
        };
    }
}
