using System.Net;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Http;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfigureResource;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class StagingResourceConfigurationTests
{
    [Fact]
    public async Task ConfigureResource_WithProductionEndUserAndNoOnBehalfOf_ReturnsMissingOnBehalfOf()
    {
        var handler = new ConfigureResourceHandler(
            Mock.Of<IResourceRepository>(),
            Mock.Of<IAltinnResourceRepository>(),
            Mock.Of<IServiceOwnerRepository>(),
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IHostEnvironment>(),
            Options.Create(new AzureStorageOptions()),
            ForwardProof(verified: false),
            NullLogger<ConfigureResourceHandler>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "user")
        ], BrokerAuthenticationSchemes.ProductionAltinn));

        var result = await handler.Process(
            new ConfigureResourceRequest { ResourceId = "resource" },
            user,
            CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.MissingOnBehalfOf, result.AsT1);
    }

    [Fact]
    public async Task ConfigureResource_WithProductionEndUser_DoesNotAskTt02Authorization()
    {
        var authorization = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.GetServiceOwner("0192:991825827"))
            .ReturnsAsync(new ServiceOwnerEntity
            {
                Id = "0192:991825827",
                Name = "Owner",
                StorageProviders = []
            });
        var resources = new Mock<IResourceRepository>();
        resources
            .Setup(repository => repository.GetResource("resource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceEntity { Id = "resource", ServiceOwnerId = "0192:991825827" });
        var handler = new ConfigureResourceHandler(
            resources.Object,
            Mock.Of<IAltinnResourceRepository>(),
            serviceOwners.Object,
            authorization.Object,
            Mock.Of<IHostEnvironment>(),
            Options.Create(new AzureStorageOptions()),
            ForwardProof(verified: true),
            NullLogger<ConfigureResourceHandler>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "user")
        ], BrokerAuthenticationSchemes.ProductionAltinn));

        var result = await handler.Process(
            new ConfigureResourceRequest { ResourceId = "resource", OnBehalfOf = "991825827" },
            user,
            CancellationToken.None);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task ConfigureResource_WithProductionEndUserAndNoForwardProof_ReturnsNoPublishAccess()
    {
        var authorization = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var serviceOwners = new Mock<IServiceOwnerRepository>();
        serviceOwners
            .Setup(repository => repository.GetServiceOwner("0192:991825827"))
            .ReturnsAsync(new ServiceOwnerEntity
            {
                Id = "0192:991825827",
                Name = "Owner",
                StorageProviders = []
            });
        var resources = new Mock<IResourceRepository>();
        resources
            .Setup(repository => repository.GetResource("resource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceEntity { Id = "resource", ServiceOwnerId = "0192:991825827" });
        var handler = new ConfigureResourceHandler(
            resources.Object,
            Mock.Of<IAltinnResourceRepository>(),
            serviceOwners.Object,
            authorization.Object,
            Mock.Of<IHostEnvironment>(),
            Options.Create(new AzureStorageOptions()),
            ForwardProof(verified: false),
            NullLogger<ConfigureResourceHandler>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "user")
        ], BrokerAuthenticationSchemes.ProductionAltinn));

        var result = await handler.Process(
            new ConfigureResourceRequest { ResourceId = "resource", OnBehalfOf = "991825827" },
            user,
            CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(Errors.NoPublishAccessToConfigureResource, result.AsT1);
    }

    [Fact]
    public async Task AuthorizeStagingPublish_AsksProductionAuthorization()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.CheckAccessAsPublisher(
                It.IsAny<ClaimsPrincipal?>(),
                "digdir-broker-administrasjon",
                "991825827",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new ConfigureResourceHandler(
            Mock.Of<IResourceRepository>(),
            Mock.Of<IAltinnResourceRepository>(),
            Mock.Of<IServiceOwnerRepository>(),
            authorization.Object,
            Mock.Of<IHostEnvironment>(),
            Options.Create(new AzureStorageOptions()),
            ForwardProof(verified: false),
            NullLogger<ConfigureResourceHandler>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "user")
        ], BrokerAuthenticationSchemes.EndUserCookie));

        var error = await handler.AuthorizeStagingPublish("991825827", user, CancellationToken.None);

        Assert.Null(error);
        authorization.Verify(
            service => service.CheckAccessAsPublisher(
                user,
                "digdir-broker-administrasjon",
                "991825827",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForwardAsync_SendsTheProductionTokenToTheTt02Resource()
    {
        HttpRequestMessage? captured = null;
        var httpHandler = new RecordingHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"maxFileTransferSize":1}""", Encoding.UTF8, "application/json")
            };
        });
        using var client = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://ab.tt02.altinn.no/")
        };
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.Setup(environment => environment.EnvironmentName).Returns(Environments.Production);
        var proxy = new StagingResourceConfigurationProxy(
            client,
            Mock.Of<IEndUserTokenProvider>(),
            Options.Create(new AltinnOptions
            {
                StagingBrokerApiUrl = "https://ab.tt02.altinn.no",
                StagingConfigurationForwardKey = "forward-key"
            }),
            hostEnvironment.Object,
            NullLogger<StagingResourceConfigurationProxy>.Instance);

        Assert.True(proxy.IsEnabled);

        var result = await proxy.ForwardAsync(
            HttpMethod.Put.Method,
            "my resource",
            "123456789",
            new ResourceExt { MaxFileTransferSize = 10 },
            "production-token",
            CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal("/broker/api/v1/resource/my%20resource", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("?onBehalfOf=123456789", captured.RequestUri.Query);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("production-token", captured.Headers.Authorization?.Parameter);
        Assert.True(captured.Headers.TryGetValues(ProductionConfigurationForwardProof.HeaderName, out var forwardKeys));
        Assert.Equal("forward-key", Assert.Single(forwardKeys));
    }

    [Fact]
    public void ForwardProof_AcceptsOnlyTheConfiguredKey()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ProductionConfigurationForwardProof.HeaderName] = "forward-key";
        var accessor = new HttpContextAccessor { HttpContext = context };

        var proof = new ProductionConfigurationForwardProof(
            accessor,
            Options.Create(new AltinnOptions { StagingConfigurationForwardKey = "forward-key" }));
        Assert.True(proof.IsVerified);

        context.Request.Headers[ProductionConfigurationForwardProof.HeaderName] = "other-key";
        Assert.False(proof.IsVerified);

        var unconfigured = new ProductionConfigurationForwardProof(
            accessor,
            Options.Create(new AltinnOptions()));
        Assert.False(unconfigured.IsVerified);
    }

    private static IProductionConfigurationForwardProof ForwardProof(bool verified)
    {
        var proof = new Mock<IProductionConfigurationForwardProof>();
        proof.SetupGet(item => item.IsVerified).Returns(verified);
        return proof.Object;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}
