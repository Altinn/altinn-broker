using System.Net.Http.Headers;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Tests.Helpers;

using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.States;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Moq;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    internal Mock<IBackgroundJobClient>? HangfireBackgroundJobClient;
    internal Mock<IRecurringJobManager>? HangfireRecurringJobClient;
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Overwrite registrations from Program.cs
        builder.ConfigureTestServices((services) =>
        {
            var authenticationBuilder = services.AddAuthentication();
            authenticationBuilder.Services.Configure<AuthenticationOptions>(o =>
            {
                o.SchemeMap.Clear();
                ((IList<AuthenticationSchemeBuilder>)o.Schemes).Clear();
            });
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = false,
                        RequireExpirationTime = false,
                        RequireSignedTokens = false,
                        SignatureValidator = delegate (string token, TokenValidationParameters parameters)
                        {
                            var jwt = new JsonWebToken(token);
                            return jwt;
                        }
                    };
                }).AddJwtBearer(AuthorizationConstants.Legacy, options => // To support "overgangslosningen"
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = false,
                        RequireExpirationTime = false,
                        RequireSignedTokens = false,
                        SignatureValidator = delegate (string token, TokenValidationParameters parameters)
                        {
                            var jwt = new JsonWebToken(token);
                            return jwt;
                        }
                    };
                });

            services.AddHangfire(config =>
                config.UseMemoryStorage()
            );
            HangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            HangfireBackgroundJobClient.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("1");
            services.AddSingleton(HangfireBackgroundJobClient.Object);
            HangfireRecurringJobClient = new Mock<IRecurringJobManager>();
            services.AddSingleton(HangfireRecurringJobClient.Object);

            var altinnResourceRepository = new Mock<IAltinnResourceRepository>();
            altinnResourceRepository.Setup(x => x.GetResource(It.Is<string>("altinn-broker-test-resource-1", StringComparer.Ordinal), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResourceEntity
                {
                    Id = "altinn-broker-test-resource-1",
                    Created = DateTime.UtcNow,
                    ServiceOwnerId = $"0192:991825827",
                    OrganizationNumber = "991825827",
                });
            altinnResourceRepository.Setup(x => x.GetResource(It.Is<string>(TestConstants.RESOURCE_WITH_NO_SERVICE_OWNER, StringComparer.Ordinal), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResourceEntity
                {
                    Id = TestConstants.RESOURCE_WITH_NO_SERVICE_OWNER,
                    Created = DateTime.UtcNow,
                    ServiceOwnerId = "",
                    OrganizationNumber = "",
                });
            services.AddSingleton(altinnResourceRepository.Object);

            var authorizationService = new Mock<IAuthorizationService>();
            authorizationService.Setup(x => x.CheckUserAccess(It.IsAny<string>(), It.IsAny<List<ResourceAccessLevel>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            authorizationService.Setup(x => x.CheckUserAccess(TestConstants.RESOURCE_WITH_NO_ACCESS, It.IsAny<List<ResourceAccessLevel>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            services.AddSingleton(authorizationService.Object);

            var eventBus = new Mock<IEventBus>();
            services.AddSingleton(eventBus.Object);
        });
    }

    public HttpClient CreateClientWithAuthorization(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
