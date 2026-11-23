using System.Security.Claims;

using Altinn.Broker.Application;
using Altinn.Broker.Application.ProvisionServiceOwner;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class ServiceOwnerProvisioningHandlerTests
{
    private const string Party = "991825827";
    private const string ServiceOwnerId = "0192:991825827";

    [Fact]
    public async Task Start_RegistersTheOwnerAndDeploysStorage()
    {
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.TryInitializeServiceOwner(ServiceOwnerId, "Digitaliseringsdirektoratet"))
            .ReturnsAsync(true);
        serviceOwners
            .Setup(repository => repository.GetServiceOwner(ServiceOwnerId))
            .ReturnsAsync(QueuedOwner());
        var resourceManager = new Mock<IResourceManager>();
        var handler = CreateStartHandler(serviceOwners, resourceManager, hasGatekeeper: true);

        var result = await handler.Process(Request(), CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ServiceOwnerProvisioningStatus.InProgress, result.AsT0.Status);
        Assert.Equal(ProvisioningPhase.Queued, result.AsT0.Phase);
        Assert.Equal(10, result.AsT0.Percent);
        resourceManager.Verify(manager => manager.CreateStorageProviders(It.IsAny<ServiceOwnerEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_WhenDeploymentIsAlreadyRunning_DoesNotStartAnother()
    {
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.TryInitializeServiceOwner(ServiceOwnerId, "Digitaliseringsdirektoratet"))
            .ReturnsAsync(false);
        var running = QueuedOwner();
        running.ProvisioningPhase = ProvisioningPhase.CreatingVirusScanStorage;
        serviceOwners
            .Setup(repository => repository.GetServiceOwner(ServiceOwnerId))
            .ReturnsAsync(running);
        var resourceManager = new Mock<IResourceManager>();
        var handler = CreateStartHandler(serviceOwners, resourceManager, hasGatekeeper: true);

        var result = await handler.Process(Request(), CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ProvisioningPhase.CreatingVirusScanStorage, result.AsT0.Phase);
        Assert.Equal(45, result.AsT0.Percent);
        resourceManager.Verify(manager => manager.CreateStorageProviders(It.IsAny<ServiceOwnerEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_WhenDeploymentFailed_StartsItAgain()
    {
        var failed = QueuedOwner();
        failed.ProvisioningPhase = ProvisioningPhase.Failed;
        var queued = QueuedOwner();
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.TryInitializeServiceOwner(ServiceOwnerId, "Digitaliseringsdirektoratet"))
            .ReturnsAsync(false);
        serviceOwners
            .SetupSequence(repository => repository.GetServiceOwner(ServiceOwnerId))
            .ReturnsAsync(failed)
            .ReturnsAsync(queued);
        var resourceManager = new Mock<IResourceManager>();
        var handler = CreateStartHandler(serviceOwners, resourceManager, hasGatekeeper: true);

        var result = await handler.Process(Request(), CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ServiceOwnerProvisioningStatus.InProgress, result.AsT0.Status);
        serviceOwners.Verify(repository => repository.SetProvisioningPhase(ServiceOwnerId, ProvisioningPhase.Queued), Times.Once);
        resourceManager.Verify(manager => manager.CreateStorageProviders(It.IsAny<ServiceOwnerEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_WithoutGatekeeperAccess_DoesNotRegisterTheOwner()
    {
        var serviceOwners = new Mock<IServiceOwnerRepository>(MockBehavior.Strict);
        var resourceManager = new Mock<IResourceManager>(MockBehavior.Strict);
        var handler = CreateStartHandler(serviceOwners, resourceManager, hasGatekeeper: false);

        var result = await handler.Process(Request(), CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoPublishAccessToConfigureResource, result.AsT1);
    }

    [Fact]
    public async Task Start_WithoutAnOwnedBrokerService_StillRegistersTheOwner()
    {
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.TryInitializeServiceOwner(ServiceOwnerId, "Digitaliseringsdirektoratet"))
            .ReturnsAsync(true);
        serviceOwners
            .Setup(repository => repository.GetServiceOwner(ServiceOwnerId))
            .ReturnsAsync(QueuedOwner());
        var resourceManager = new Mock<IResourceManager>();
        var handler = CreateStartHandler(serviceOwners, resourceManager, hasGatekeeper: true);

        var result = await handler.Process(Request(), CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ServiceOwnerProvisioningStatus.InProgress, result.AsT0.Status);
        resourceManager.Verify(manager => manager.CreateStorageProviders(It.IsAny<ServiceOwnerEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WhenTheOwnerIsNotRegistered_ReturnsNotRegistered()
    {
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.GetServiceOwner(ServiceOwnerId))
            .ReturnsAsync((ServiceOwnerEntity?)null);
        var handler = CreateGetHandler(serviceOwners, hasGatekeeper: true);

        var result = await handler.Process(Party, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ServiceOwnerProvisioningStatus.NotRegistered, result.AsT0.Status);
        Assert.Null(result.AsT0.Phase);
        Assert.Equal(0, result.AsT0.Percent);
    }

    private static StartServiceOwnerProvisioningHandler CreateStartHandler(
        Mock<IServiceOwnerRepository> serviceOwners,
        Mock<IResourceManager> resourceManager,
        bool hasGatekeeper)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.CheckAccessAsPublisher(
                It.IsAny<ClaimsPrincipal?>(),
                ApplicationConstants.BrokerBoxConfigureGatekeeperResourceId,
                Party,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasGatekeeper);

        return new StartServiceOwnerProvisioningHandler(
            serviceOwners.Object,
            authorization.Object,
            resourceManager.Object,
            Mock.Of<IHostEnvironment>(),
            NullLogger<StartServiceOwnerProvisioningHandler>.Instance);
    }

    private static GetServiceOwnerProvisioningHandler CreateGetHandler(Mock<IServiceOwnerRepository> serviceOwners, bool hasGatekeeper)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.CheckAccessAsPublisher(
                It.IsAny<ClaimsPrincipal?>(),
                ApplicationConstants.BrokerBoxConfigureGatekeeperResourceId,
                Party,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasGatekeeper);
        return new GetServiceOwnerProvisioningHandler(serviceOwners.Object, authorization.Object, Mock.Of<IHostEnvironment>());
    }

    private static StartServiceOwnerProvisioningRequest Request() => new()
    {
        Party = Party,
        Name = "Digitaliseringsdirektoratet"
    };

    private static ServiceOwnerEntity QueuedOwner() => new()
    {
        Id = ServiceOwnerId,
        Name = "Digitaliseringsdirektoratet",
        StorageProviders = [],
        ProvisioningPhase = ProvisioningPhase.Queued
    };

    private static ClaimsPrincipal CreateUser()
        => new(new ClaimsIdentity([new Claim("urn:altinn:userid", "12345")], "Test"));
}
