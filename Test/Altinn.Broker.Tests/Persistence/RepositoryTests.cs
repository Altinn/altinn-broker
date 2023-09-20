using Altinn.Broker.Core.Domain;
using Altinn.Broker.Persistence.Repositories;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Tests.Persistence;

public class RepositoryTests
{
    private readonly string DATABASE_CONNECTION_STRING = "Host=localhost:5432;Username=postgres;Password=postgres;Database=broker";
    private FileRepository _fileRepository;
    private ActorRepository _actorRepository;
    private ShipmentRepository _shipmentRepository;

    public RepositoryTests()
    {
        _fileRepository = new FileRepository(DATABASE_CONNECTION_STRING);
        _actorRepository = new ActorRepository(DATABASE_CONNECTION_STRING);
        _shipmentRepository = new ShipmentRepository(DATABASE_CONNECTION_STRING);
    }

    [Fact]
    public void AddFileStorageReference_WhenAddFile_ShouldBeCreated()
    {
        // Arrange
        Guid fileId = Guid.NewGuid();
        string fileLocation = "path/to/file" + fileId.ToString();

        var senderActorId = new Random().Next(0, 10000000);
        _actorRepository.AddActor(new Actor()
        {
            ActorId = senderActorId,
            ActorExternalId = "1"
        });

        Guid shipmentId = Guid.NewGuid();
        _shipmentRepository.AddShipment(new Shipment()
        {
            ShipmentId = shipmentId,
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            Initiated = DateTime.UtcNow,
            ExternalShipmentReference = "1",
            Receipts = new List<ShipmentReceipt>(),
            UploaderActorId = senderActorId
        });


        // Act
        _fileRepository.AddFile(new Core.Domain.File()
        {
            ShipmentId = shipmentId,
            FileId = fileId,
            ExternalFileReference = "1",
            FileStatus = Core.Domain.Enums.FileStatus.Ready,
            FileLocation = fileLocation,
            Uploaded = DateTime.UtcNow,
            LastStatusUpdate = DateTime.UtcNow,
            Receipts = new List<FileReceipt>(),
        });

        // Assert
        var savedFile = _fileRepository.GetFile(fileId);
        Assert.Equal(fileLocation, savedFile.FileLocation);
    }

    [Fact]
    public void AddReceipt_WhenCalled_ShouldSaveReceipt()
    {
        // Arrange
        var senderActorId = new Random().Next(0, 10000000);
        _actorRepository.AddActor(new Actor()
        {
            ActorId = senderActorId,
            ActorExternalId = "1"
        });

        var recipientActorId = new Random().Next(0, 10000000);
        _actorRepository.AddActor(new Actor()
        {
            ActorId = recipientActorId,
            ActorExternalId = "1"
        });

        Guid shipmentId = Guid.NewGuid();
        _shipmentRepository.AddShipment(new Shipment()
        {
            ShipmentId = shipmentId,
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            Initiated = DateTime.UtcNow,
            ExternalShipmentReference = "1",
            Receipts = new List<ShipmentReceipt>(),
            UploaderActorId = senderActorId
        });

        Guid fileId = Guid.NewGuid();
        _fileRepository.AddFile(new Core.Domain.File()
        {
            ShipmentId = shipmentId,
            FileId = fileId,
            ExternalFileReference = "1",
            FileStatus = Core.Domain.Enums.FileStatus.Ready,
            Uploaded = DateTime.UtcNow,
            FileLocation = "path/to/file",
            LastStatusUpdate = DateTime.UtcNow,
            Receipts = new List<FileReceipt>(),
        });

        // Act
        _fileRepository.AddReceipt(new FileReceipt()
        {
            Actor = new Actor()
            {
                ActorId = recipientActorId,
                ActorExternalId = "1",
            },
            Date = DateTime.UtcNow,
            FileId = fileId,
            Status = Core.Domain.Enums.ActorFileStatus.Uploaded
        });

        // Assert
        var savedFile = _fileRepository.GetFile(fileId);
        Assert.NotNull(savedFile);
        Assert.NotEmpty(savedFile.Receipts);
        Assert.Equal(1, savedFile.Receipts.Count);
        Assert.Equal(Core.Domain.Enums.ActorFileStatus.Uploaded, savedFile.Receipts.First().Status);
    }

    [Fact]
    public void GetFile_ExistingFileId_ShouldReturnFile()
    {
        // Arrange
        var senderActorId = new Random().Next(0, 10000000);
        _actorRepository.AddActor(new Actor()
        {
            ActorId = senderActorId,
            ActorExternalId = "1"
        });

        Guid shipmentId = Guid.NewGuid();
        _shipmentRepository.AddShipment(new Shipment()
        {
            ShipmentId = shipmentId,
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            Initiated = DateTime.UtcNow,
            ExternalShipmentReference = "1",
            Receipts = new List<ShipmentReceipt>(),
            UploaderActorId = senderActorId
        });

        Guid fileId = Guid.NewGuid();
        _fileRepository.AddFile(new Core.Domain.File()
        {
            ShipmentId = shipmentId,
            FileId = fileId,
            ExternalFileReference = "1",
            FileStatus = Core.Domain.Enums.FileStatus.Ready,
            Uploaded = DateTime.UtcNow,
            FileLocation = "path/to/file",
            LastStatusUpdate = DateTime.UtcNow,
            Receipts = new List<FileReceipt>(),
        });

        // Act
        var result = _fileRepository.GetFile(fileId);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetFile_NonExistingFileId_ShouldReturnNull()
    {
        // Arrange
        // Act
        var result = _fileRepository.GetFile(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SaveShipment_Successful_CanRetrieveShipment()
    {
        // Arrange
        var actor = new Actor()
        {
            ActorId = new Random().Next(0, 10000000),
            ActorExternalId = Guid.NewGuid().ToString(),
        };
        _actorRepository.AddActor(actor);
        var shipment = new Shipment()
        {
            ExternalShipmentReference = Guid.NewGuid().ToString(),
            Initiated = DateTime.Now.ToUniversalTime(),
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            UploaderActorId = actor.ActorId,
            ShipmentId = Guid.NewGuid(),
            Receipts = new List<ShipmentReceipt>()
        };

        // Act
        _shipmentRepository.AddShipment(shipment);
        var savedShipment = _shipmentRepository.GetShipment(shipment.ShipmentId);

        // Assert
        Assert.Equal(shipment.ShipmentId, savedShipment?.ShipmentId);
    }

    [Fact]
    public void SaveActor_Successful_CanRetrieveActor()
    {
        // Arrange
        var actor = new Actor()
        {
            ActorId = new Random().Next(0, 10000000),
            ActorExternalId = Guid.NewGuid().ToString()
        };

        // Act
        _actorRepository.AddActor(actor);
        var savedActor = _actorRepository.GetActor(actor.ActorId);

        // Assert
        Assert.Equal(actor.ActorExternalId, savedActor.ActorExternalId);
    }
}
