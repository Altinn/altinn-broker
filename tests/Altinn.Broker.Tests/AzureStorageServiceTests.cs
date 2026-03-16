using System.Text;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Integrations.Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Broker.Tests;

public class AzureStorageServiceTests
{
    [Fact]
    public async Task UploadFile_WhenTotalBlocksEqualsBlocksBeforeCommit_FinalCommitIsNotMarkedAsFirst()
    {
        // Arrange
        var azureOptions = Options.Create(new AzureStorageOptions
        {
            BlockSize = 4,
            ConcurrentUploadThreads = 4,
            BlocksBeforeCommit = 2
        });

        var reportOptions = Options.Create(new ReportStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true"
        });

        var mockEnvironment = new Mock<IHostEnvironment>();
        var mockLogger = new Mock<ILogger<AzureStorageService>>();
        var service = new TestAzureStorageService(azureOptions, reportOptions, mockEnvironment.Object, mockLogger.Object);

        var serviceOwner = CreateDefaultServiceOwner();
        var fileTransfer = CreateDefaultFileTransfer();

        // We want exactly BlocksBeforeCommit blocks, each flushed separately.
        var totalBlocks = azureOptions.Value.BlocksBeforeCommit;
        var totalBytes = azureOptions.Value.BlockSize * totalBlocks;
        using var stream = new ChunkedStream(
            Encoding.UTF8.GetBytes(new string('a', totalBytes)),
            azureOptions.Value.BlockSize);

        // Act
        await service.UploadFile(serviceOwner, fileTransfer, stream, CancellationToken.None);

        // Assert
        // Exactly two commits: first creates the blob, second must NOT be marked as first.
        Assert.Equal(2, service.FirstCommitFlags.Count);
        Assert.True(service.FirstCommitFlags[0]);
        Assert.False(service.FirstCommitFlags[1]);
    }

    [Fact]
    public async Task UploadFile_WhenTotalBlocksLessThanBlocksBeforeCommit_SingleFirstCommit()
    {
        var azureOptions = Options.Create(new AzureStorageOptions
        {
            BlockSize = 4,
            ConcurrentUploadThreads = 4,
            BlocksBeforeCommit = 3
        });

        var reportOptions = Options.Create(new ReportStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true"
        });

        var mockEnvironment = new Mock<IHostEnvironment>();
        var mockLogger = new Mock<ILogger<AzureStorageService>>();
        var service = new TestAzureStorageService(azureOptions, reportOptions, mockEnvironment.Object, mockLogger.Object);

        var serviceOwner = CreateDefaultServiceOwner();
        var fileTransfer = CreateDefaultFileTransfer();

        // Two blocks, but commit threshold is three, so only final commit should happen.
        var totalBlocks = azureOptions.Value.BlocksBeforeCommit - 1;
        var totalBytes = azureOptions.Value.BlockSize * totalBlocks;
        using var stream = new ChunkedStream(
            Encoding.UTF8.GetBytes(new string('b', totalBytes)),
            azureOptions.Value.BlockSize);

        await service.UploadFile(serviceOwner, fileTransfer, stream, CancellationToken.None);

        Assert.Single(service.FirstCommitFlags);
        Assert.True(service.FirstCommitFlags[0]);
    }

    [Fact]
    public async Task UploadFile_WhenTotalBlocksGreaterThanBlocksBeforeCommit_WithRemainder_FirstCommitOnlyOnce()
    {
        var azureOptions = Options.Create(new AzureStorageOptions
        {
            BlockSize = 4,
            ConcurrentUploadThreads = 4,
            BlocksBeforeCommit = 2
        });

        var reportOptions = Options.Create(new ReportStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true"
        });

        var mockEnvironment = new Mock<IHostEnvironment>();
        var mockLogger = new Mock<ILogger<AzureStorageService>>();
        var service = new TestAzureStorageService(azureOptions, reportOptions, mockEnvironment.Object, mockLogger.Object);

        var serviceOwner = CreateDefaultServiceOwner();
        var fileTransfer = CreateDefaultFileTransfer();

        // Three blocks: one intermediate commit after two blocks, one final commit after the last block.
        var totalBlocks = azureOptions.Value.BlocksBeforeCommit + 1;
        var totalBytes = azureOptions.Value.BlockSize * totalBlocks;
        using var stream = new ChunkedStream(
            Encoding.UTF8.GetBytes(new string('c', totalBytes)),
            azureOptions.Value.BlockSize);

        await service.UploadFile(serviceOwner, fileTransfer, stream, CancellationToken.None);

        Assert.Equal(2, service.FirstCommitFlags.Count);
        Assert.True(service.FirstCommitFlags[0]);
        Assert.False(service.FirstCommitFlags[1]);
    }

    [Fact]
    public async Task UploadFile_WhenFileFitsInSingleBlock_SingleFirstCommit()
    {
        var azureOptions = Options.Create(new AzureStorageOptions
        {
            BlockSize = 1024,
            ConcurrentUploadThreads = 2,
            BlocksBeforeCommit = 10
        });

        var reportOptions = Options.Create(new ReportStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true"
        });

        var mockEnvironment = new Mock<IHostEnvironment>();
        var mockLogger = new Mock<ILogger<AzureStorageService>>();
        var service = new TestAzureStorageService(azureOptions, reportOptions, mockEnvironment.Object, mockLogger.Object);

        var serviceOwner = CreateDefaultServiceOwner();
        var fileTransfer = CreateDefaultFileTransfer();

        // Smaller than a single block -> one block, one commit.
        var totalBytes = azureOptions.Value.BlockSize / 2;
        using var stream = new ChunkedStream(
            Encoding.UTF8.GetBytes(new string('d', totalBytes)),
            totalBytes);

        await service.UploadFile(serviceOwner, fileTransfer, stream, CancellationToken.None);

        Assert.Single(service.FirstCommitFlags);
        Assert.True(service.FirstCommitFlags[0]);
    }

    private static ServiceOwnerEntity CreateDefaultServiceOwner() => new()
    {
        Id = "test",
        Name = "test-owner",
        StorageProviders =
        [
            new StorageProviderEntity
            {
                Id = 1,
                Created = DateTimeOffset.UtcNow,
                Type = StorageProviderType.Altinn3Azure,
                ResourceName = "devstoreaccount1",
                ServiceOwnerId = "test",
                Active = true
            }
        ]
    };

    private static FileTransferEntity CreateDefaultFileTransfer()
    {
        var fileTransferId = Guid.NewGuid();
        return new FileTransferEntity
        {
            FileTransferId = fileTransferId,
            ResourceId = "resource",
            Sender = new ActorEntity { ActorExternalId = "sender" },
            FileTransferStatusEntity = new FileTransferStatusEntity
            {
                FileTransferId = fileTransferId,
                Date = DateTimeOffset.UtcNow,
                DetailedStatus = "test",
                Status = FileTransferStatus.Published
            },
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.AddHours(1),
            RecipientCurrentStatuses =
            [
                new ActorFileTransferStatusEntity
                {
                    Actor = new ActorEntity { ActorExternalId = "recipient" },
                    Date = DateTimeOffset.UtcNow,
                    FileTransferId = fileTransferId
                }
            ],
            FileName = "test.txt"
        };
    }

    private sealed class TestAzureStorageService : AzureStorageService
    {
        public List<bool> FirstCommitFlags { get; } = [];

        public TestAzureStorageService(
            IOptions<AzureStorageOptions> azureStorageOptions,
            IOptions<ReportStorageOptions> reportStorageOptions,
            IHostEnvironment hostEnvironment,
            ILogger<AzureStorageService> logger)
            : base(azureStorageOptions, reportStorageOptions, hostEnvironment, logger)
        {
        }

        protected override Task<BlobContainerClient> GetBlobContainerClient(
            FileTransferEntity fileTransferEntity,
            ServiceOwnerEntity serviceOwnerEntity)
        {
            // Create a BlobContainerClient without performing any network I/O.
            var serviceUri = new Uri("https://unit-test-account.blob.core.windows.net");
            var blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
            var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles-test");
            return Task.FromResult(containerClient);
        }

        protected override Task UploadBlock(BlockBlobClient client, string blockId, byte[] blockData, CancellationToken cancellationToken)
        {
            // Avoid any real network I/O in tests
            return Task.CompletedTask;
        }

        protected override Task CommitBlocks(BlockBlobClient client, List<string> blockList, bool firstCommit, byte[]? finalMd5,
            CancellationToken cancellationToken)
        {
            FirstCommitFlags.Add(firstCommit);
            // Avoid real network I/O in tests
            return Task.CompletedTask;
        }
    }

    private sealed class ChunkedStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _chunkSize;
        private int _position;

        public ChunkedStream(byte[] data, int chunkSize)
        {
            _data = data;
            _chunkSize = chunkSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _data.Length - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            var toCopy = Math.Min(Math.Min(count, _chunkSize), remaining);
            Array.Copy(_data, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

