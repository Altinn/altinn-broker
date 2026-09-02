using Altinn.Broker.Application;
using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Hangfire;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class InitializeFileTransferHandlerTests
{
    [Fact]
    public async Task Process_InvalidRecipient_ReturnsBadRequestError()
    {
        const string resourceId = "access-list-resource";
        var resource = new ResourceEntity
        {
            Id = resourceId,
            ServiceOwnerId = "0192:991825827"
        };
        var altinnResource = new ResourceEntity
        {
            Id = resourceId,
            ServiceOwnerId = "0192:991825827",
            AccessListEnabled = true
        };

        var resourceRepository = new Mock<IResourceRepository>();
        resourceRepository
            .Setup(repository => repository.GetResource(resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resource);

        var altinnResourceRepository = new Mock<IAltinnResourceRepository>();
        altinnResourceRepository
            .Setup(repository => repository.GetResource(resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(altinnResource);
        altinnResourceRepository
            .Setup(repository => repository.GetAccessListOfResource(resourceId, "111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string>?)null);

        var serviceOwnerRepository = new Mock<IServiceOwnerRepository>();
        serviceOwnerRepository
            .Setup(repository => repository.GetServiceOwner(resource.ServiceOwnerId))
            .ReturnsAsync(new ServiceOwnerEntity
            {
                Id = resource.ServiceOwnerId,
                Name = "Test owner",
                StorageProviders =
                [
                    new StorageProviderEntity
                    {
                        Type = StorageProviderType.Altinn3Azure,
                        ResourceName = "test-storage",
                        ServiceOwnerId = resource.ServiceOwnerId,
                        Active = true
                    }
                ]
            });

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.CheckAccessAsSender(null, resourceId, "0192:991825827", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(environment => environment.EnvironmentName).Returns(Environments.Development);

        var fileTransferRepository = new Mock<IFileTransferRepository>();
        var eventBus = new EventBusMiddleware(new Mock<IEventBus>().Object);
        var handler = new InitializeFileTransferHandler(
            resourceRepository.Object,
            altinnResourceRepository.Object,
            serviceOwnerRepository.Object,
            authorizationService.Object,
            fileTransferRepository.Object,
            new Mock<IFileTransferStatusRepository>().Object,
            new Mock<IActorFileTransferStatusRepository>().Object,
            new Mock<IBackgroundJobClient>().Object,
            eventBus,
            hostEnvironment.Object,
            new Mock<IAltinnRegisterService>().Object,
            NullLogger<InitializeFileTransferHandler>.Instance);

        var result = await handler.Process(new InitializeFileTransferRequest
        {
            ResourceId = resourceId,
            FileName = "test.txt",
            SendersFileTransferReference = "reference",
            SenderExternalId = "0192:991825827",
            RecipientExternalIds = ["0192:111111111"],
            PropertyList = new Dictionary<string, string>()
        }, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.InvalidRecipient, result.AsT1);
        fileTransferRepository.Verify(
            repository => repository.AddFileTransfer(
                It.IsAny<ResourceEntity>(),
                It.IsAny<StorageProviderEntity>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
