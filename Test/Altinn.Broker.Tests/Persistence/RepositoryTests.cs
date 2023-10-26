using Altinn.Broker.Core.Domain;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;
using Altinn.Broker.Persistence.Repositories;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Tests.Persistence;

public class RepositoryTests
{
    private readonly string DATABASE_CONNECTION_STRING = "Host=localhost:5432;Username=postgres;Password=postgres;Database=broker";
    private readonly DatabaseConnectionProvider _databaseConnectionProvider;
    private FileRepository _fileRepository;
    private ActorRepository _actorRepository;
    private ShipmentRepository _shipmentRepository;

    public RepositoryTests()
    {
        IOptions<DatabaseOptions> databaseOptions = Options.Create(new DatabaseOptions()
        {
            ConnectionString = DATABASE_CONNECTION_STRING
        });
        _databaseConnectionProvider = new DatabaseConnectionProvider(databaseOptions);

        _fileRepository = new FileRepository(_databaseConnectionProvider);
        _actorRepository = new ActorRepository(_databaseConnectionProvider);
        _shipmentRepository = new ShipmentRepository(_databaseConnectionProvider);
    }

    [Fact]
    public async Task AddFileStorageReference_WhenAddFile_ShouldBeCreated()
    {
        // Arrange
        Guid fileId = Guid.NewGuid();
        string fileLocation = "path/to/file" + fileId.ToString();

        var senderActorId = new Random().Next(0, 10000000);
        await _actorRepository.AddActorAsync(new Actor()
        {
            ActorId = senderActorId,
            ActorExternalId = "1"
        });

        Guid shipmentId = Guid.NewGuid();
        await _shipmentRepository.AddShipmentAsync(new Shipment()
        {
            ShipmentId = shipmentId,
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            Initiated = DateTime.UtcNow,
            ExternalShipmentReference = "1",
            Receipts = new List<ShipmentReceipt>(),
            UploaderActorId = senderActorId
        });


        // Act
        await _fileRepository.AddFileAsync(new Core.Domain.File()
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
        var savedFile = await _fileRepository.GetFileAsync(fileId);
        Assert.Equal(fileLocation, savedFile.FileLocation);
    }

    [Fact]
    public async Task AddReceipt_WhenCalled_ShouldSaveReceipt()
    {
        // Arrange
        var senderActorId = new Random().Next(0, 10000000);
        await _actorRepository.AddActorAsync(new Actor()
        {
            ActorId = senderActorId,
            ActorExternalId = "1"
        });

        var recipientActorId = new Random().Next(0, 10000000);
        await _actorRepository.AddActorAsync(new Actor()
        {
            ActorId = recipientActorId,
            ActorExternalId = "1"
        });

        Guid shipmentId = Guid.NewGuid();
        await _shipmentRepository.AddShipmentAsync(new Shipment()
        {
            ShipmentId = shipmentId,
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            Initiated = DateTime.UtcNow,
            ExternalShipmentReference = "1",
            Receipts = new List<ShipmentReceipt>(),
            UploaderActorId = senderActorId
        });

        Guid fileId = Guid.NewGuid();
        await _fileRepository.AddFileAsync(new Core.Domain.File()
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
        await _fileRepository.AddReceiptAsync(new FileReceipt()
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
        var savedFile = await _fileRepository.GetFileAsync(fileId);
        Assert.NotNull(savedFile);
        Assert.NotEmpty(savedFile.Receipts);
        Assert.Single(savedFile.Receipts);
        Assert.Equal(Core.Domain.Enums.ActorFileStatus.Uploaded, savedFile.Receipts.First().Status);
    }

    [Fact]
    public async Task GetFile_ExistingFileId_ShouldReturnFile()
    {
        // Arrange
        var senderActorId = new Random().Next(0, 10000000);
        await _actorRepository.AddActorAsync(new Actor()
        {
            ActorId = senderActorId,
            ActorExternalId = "1"
        });

        Guid shipmentId = Guid.NewGuid();
        await _shipmentRepository.AddShipmentAsync(new Shipment()
        {
            ShipmentId = shipmentId,
            ShipmentStatus = Core.Domain.Enums.ShipmentStatus.Initialized,
            Initiated = DateTime.UtcNow,
            ExternalShipmentReference = "1",
            Receipts = new List<ShipmentReceipt>(),
            UploaderActorId = senderActorId
        });

        Guid fileId = Guid.NewGuid();
        await _fileRepository.AddFileAsync(new Core.Domain.File()
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
        var result = await _fileRepository.GetFileAsync(fileId);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFile_NonExistingFileId_ShouldReturnNull()
    {
        // Arrange
        // Act
        var result = await _fileRepository.GetFileAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveShipment_Successful_CanRetrieveShipment()
    {
        // Arrange
        var actor = new Actor()
        {
            ActorId = new Random().Next(0, 10000000),
            ActorExternalId = Guid.NewGuid().ToString(),
        };
        await _actorRepository.AddActorAsync(actor);
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
        await _shipmentRepository.AddShipmentAsync(shipment);
        var savedShipment = await _shipmentRepository.GetShipmentAsync(shipment.ShipmentId);

        // Assert
        Assert.Equal(shipment.ShipmentId, savedShipment?.ShipmentId);
    }

    [Fact]
    public async Task SaveActor_Successful_CanRetrieveActor()
    {
        // Arrange
        var actor = new Actor()
        {
            ActorId = new Random().Next(0, 10000000),
            ActorExternalId = Guid.NewGuid().ToString()
        };

        // Act
        await _actorRepository.AddActorAsync(actor);
        var savedActor = await _actorRepository.GetActorAsync(actor.ActorId);

        // Assert
        Assert.Equal(actor.ActorExternalId, savedActor.ActorExternalId);
    }
}