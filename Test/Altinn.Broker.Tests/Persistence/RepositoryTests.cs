﻿using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
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

    public RepositoryTests()
    {
        IOptions<DatabaseOptions> databaseOptions = Options.Create(new DatabaseOptions()
        {
            ConnectionString = DATABASE_CONNECTION_STRING
        });
        _databaseConnectionProvider = new DatabaseConnectionProvider(databaseOptions);

        _actorRepository = new ActorRepository(_databaseConnectionProvider);
        _fileRepository = new FileRepository(_databaseConnectionProvider, _actorRepository);
    }

    [Fact]
    public async Task AddFileStorageReference_WhenAddFile_ShouldBeCreated()
    {
        // Arrange
        string fileLocation = "path/to/file";


        // Act
        var fileId = await _fileRepository.AddFileAsync(new FileEntity()
        {
            ExternalFileReference = "1",
            FileStatus = Core.Domain.Enums.FileStatus.Initialized,
            FileLocation = fileLocation,
            Uploaded = DateTime.UtcNow,
            Sender = "1",
            LastStatusUpdate = DateTime.UtcNow,
            ActorEvents = new List<ActorFileStatusEntity>()
            {
                new ActorFileStatusEntity(){
                    Actor = new ActorEntity()
                    {
                        ActorExternalId= "2"
                    },
                    Date= DateTime.UtcNow,
                    Status = ActorFileStatus.Initialized
                }
            }
        });

        // Assert
        var savedFile = await _fileRepository.GetFileAsync(fileId);
        Assert.Equal(fileLocation, savedFile.FileLocation);
    }

    [Fact]
    public async Task AddReceipt_WhenCalled_ShouldSaveReceipt()
    {
        // Arrange
        Guid fileId = await _fileRepository.AddFileAsync(new Core.Domain.FileEntity()
        {
            Sender = "1",
            ExternalFileReference = "1",
            FileStatus = Core.Domain.Enums.FileStatus.Initialized,
            Uploaded = DateTime.UtcNow,
            FileLocation = "path/to/file",
            LastStatusUpdate = DateTime.UtcNow,
            ActorEvents = new List<ActorFileStatusEntity>()
            {
                new ActorFileStatusEntity(){
                    Actor = new ActorEntity()
                    {
                        ActorExternalId= "2"
                    },
                    Date= DateTime.UtcNow,
                    Status = ActorFileStatus.Initialized
                }
            }
        });

        // Act
        await _fileRepository.AddReceiptAsync(new ActorFileStatusEntity()
        {
            Actor = new ActorEntity()
            {
                ActorExternalId = "1"
            },
            Date = DateTime.UtcNow,
            FileId = fileId,
            Status = Core.Domain.Enums.ActorFileStatus.Uploaded
        });

        // Assert
        var savedFile = await _fileRepository.GetFileAsync(fileId);
        Assert.NotNull(savedFile);
        Assert.NotEmpty(savedFile.ActorEvents);
        Assert.Equal(3, savedFile.ActorEvents.Count);
    }

    [Fact]
    public async Task GetFile_ExistingFileId_ShouldReturnFile()
    {
        // Arrange
        var fileId = await _fileRepository.AddFileAsync(new Core.Domain.FileEntity()
        {
            ExternalFileReference = "1",
            FileStatus = Core.Domain.Enums.FileStatus.Initialized,
            Uploaded = DateTime.UtcNow,
            FileLocation = "path/to/file",
            Sender = "altinn",
            LastStatusUpdate = DateTime.UtcNow,
            ActorEvents = new List<ActorFileStatusEntity>()
            {
                new ActorFileStatusEntity(){
                    Actor = new ActorEntity()
                    {
                        ActorExternalId= "2"
                    },
                    Date= DateTime.UtcNow,
                    Status = ActorFileStatus.Initialized
                }
            }
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
    public async Task SaveActor_Successful_CanRetrieveActor()
    {
        // Arrange
        var actor = new ActorEntity()
        {
            ActorExternalId = Guid.NewGuid().ToString()
        };

        // Act
        var actorId = await _actorRepository.AddActorAsync(actor);
        var savedActor = await _actorRepository.GetActorAsync(actor.ActorExternalId);

        // Assert
        Assert.Equal(savedActor.ActorId, actorId);
    }
}