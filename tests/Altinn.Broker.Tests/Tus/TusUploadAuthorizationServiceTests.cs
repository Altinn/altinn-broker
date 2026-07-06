using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Tus;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusUploadAuthorizationServiceTests
{
    [Fact]
    public async Task HasActiveUploadSessionAsync_WithRecentActivity_ReturnsTrue()
    {
        var fileTransferId = Guid.NewGuid();
        var distributedCache = CreateDistributedCache();
        var activityCache = new TusUploadActivityCache(distributedCache);
        await activityCache.RecordActivityAsync(fileTransferId, CancellationToken.None);

        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(r => r.GetFileTransfer(fileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUploadStartedFileTransfer(fileTransferId, "991825827"));

        var validationService = new TusUploadValidationService(
            Mock.Of<IAuthorizationService>(),
            fileTransferRepository.Object,
            Mock.Of<IResourceRepository>(),
            Mock.Of<IServiceOwnerRepository>());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TusOptions:UploadExpiration"] = "24:00:00" })
            .Build();

        var service = new TusUploadAuthorizationService(
            distributedCache,
            activityCache,
            validationService,
            configuration,
            NullLogger<TusUploadAuthorizationService>.Instance);

        var user = TestTokenHelper.CreateMaskinportenUser("991825827");

        var hasActiveSession = await service.HasActiveUploadSessionAsync(fileTransferId, user, CancellationToken.None);

        Assert.True(hasActiveSession);
    }

    [Fact]
    public async Task HasActiveUploadSessionAsync_WithSessionCacheHit_ReturnsTrue()
    {
        var fileTransferId = Guid.NewGuid();
        var distributedCache = CreateDistributedCache();
        var activityCache = new TusUploadActivityCache(distributedCache);
        var user = TestTokenHelper.CreateMaskinportenUser("991825827");

        await distributedCache.SetStringAsync(
            $"tus-upload-auth:{fileTransferId}:test-client:991825827",
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
            CancellationToken.None);

        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(r => r.GetFileTransfer(fileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUploadStartedFileTransfer(fileTransferId, "991825827"));

        var validationService = new TusUploadValidationService(
            Mock.Of<IAuthorizationService>(),
            fileTransferRepository.Object,
            Mock.Of<IResourceRepository>(),
            Mock.Of<IServiceOwnerRepository>());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TusOptions:UploadExpiration"] = "24:00:00" })
            .Build();

        var service = new TusUploadAuthorizationService(
            distributedCache,
            activityCache,
            validationService,
            configuration,
            NullLogger<TusUploadAuthorizationService>.Instance);

        var hasActiveSession = await service.HasActiveUploadSessionAsync(fileTransferId, user, CancellationToken.None);

        Assert.True(hasActiveSession);
    }

    [Fact]
    public async Task HasActiveUploadSessionAsync_WithUploadProcessingStatus_ReturnsTrue()
    {
        var fileTransferId = Guid.NewGuid();
        var distributedCache = CreateDistributedCache();
        var activityCache = new TusUploadActivityCache(distributedCache);
        await activityCache.RecordActivityAsync(fileTransferId, CancellationToken.None);

        var fileTransferRepository = new Mock<IFileTransferRepository>();
        fileTransferRepository
            .Setup(r => r.GetFileTransfer(fileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFileTransfer(fileTransferId, "991825827", FileTransferStatus.UploadProcessing));

        var validationService = new TusUploadValidationService(
            Mock.Of<IAuthorizationService>(),
            fileTransferRepository.Object,
            Mock.Of<IResourceRepository>(),
            Mock.Of<IServiceOwnerRepository>());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TusOptions:UploadExpiration"] = "24:00:00" })
            .Build();

        var service = new TusUploadAuthorizationService(
            distributedCache,
            activityCache,
            validationService,
            configuration,
            NullLogger<TusUploadAuthorizationService>.Instance);

        var user = TestTokenHelper.CreateMaskinportenUser("991825827");

        var hasActiveSession = await service.HasActiveUploadSessionAsync(fileTransferId, user, CancellationToken.None);

        Assert.True(hasActiveSession);
    }

    private static MemoryDistributedCache CreateDistributedCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static Core.Domain.FileTransferEntity CreateUploadStartedFileTransfer(Guid fileTransferId, string senderOrgNumber)
        => CreateFileTransfer(fileTransferId, senderOrgNumber, FileTransferStatus.UploadStarted);

    private static Core.Domain.FileTransferEntity CreateFileTransfer(
        Guid fileTransferId,
        string senderOrgNumber,
        FileTransferStatus status)
    {
        var fileTransfer = FileTransferEntityFactory.BasicFileTransfer();
        fileTransfer.FileTransferId = fileTransferId;
        fileTransfer.Sender = new Core.Domain.ActorEntity { ActorExternalId = $"0192:{senderOrgNumber}" };
        fileTransfer.FileTransferStatusEntity = new Core.Domain.FileTransferStatusEntity
        {
            FileTransferId = fileTransferId,
            Status = status,
            Date = DateTime.UtcNow,
        };
        return fileTransfer;
    }
}
