using System.Net.Http.Headers;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Tests.Helpers;

using Hangfire;
using Hangfire.MemoryStorage;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Moq;

using Polly;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
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
                }).AddJwtBearer(AuthorizationConstants.LegacyAndMaskinporten, options => // To support "overgangslosningen"
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
            services.AddHangfire(c => c.UseMemoryStorage());

            var altinnResourceRepository = new Mock<IAltinnResourceRepository>();
            altinnResourceRepository.Setup(x => x.GetResourceEntity(It.Is(TestConstants.RESOURCE_FOR_TEST, StringComparer.Ordinal), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResourceEntity
                {
                    Id = TestConstants.RESOURCE_FOR_TEST,
                    Created = DateTime.UtcNow,
                    ServiceOwnerId = $"0192:991825827",
                    OrganizationNumber = "991825827",
                });
            altinnResourceRepository.Setup(x => x.GetResourceEntity(It.Is(TestConstants.RESOURCE_WITH_NO_SERVICE_OWNER, StringComparer.Ordinal), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResourceEntity
                {
                    Id = TestConstants.RESOURCE_WITH_NO_SERVICE_OWNER,
                    Created = DateTime.UtcNow,
                    ServiceOwnerId = "",
                    OrganizationNumber = "",
                });
            altinnResourceRepository.Setup(x => x.GetResourceEntity(It.Is(TestConstants.RESOURCE_WITH_GRACEFUL_PURGE, StringComparer.Ordinal), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResourceEntity
                {
                    Id = TestConstants.RESOURCE_WITH_GRACEFUL_PURGE,
                    Created = DateTime.UtcNow,
                    ServiceOwnerId = $"0192:991825827",
                    OrganizationNumber = "991825827",
                    MaxFileTransferSize = 1000000,
                    FileTransferTimeToLive = TimeSpan.FromHours(48),
                    PurgeFileTransferAfterAllRecipientsConfirmed = true,
                    PurgeFileTransferGracePeriod = TimeSpan.FromHours(24)
                });
            services.AddSingleton(altinnResourceRepository.Object);

            var authorizationService = new Mock<IAuthorizationService>();
            authorizationService.Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal>(), It.Is<string>(resource => resource == TestConstants.RESOURCE_WITH_NO_ACCESS), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            authorizationService.Setup(x => x.CheckAccessAsSender(It.IsAny<ClaimsPrincipal?>(), It.Is<string>(resource => resource != TestConstants.RESOURCE_WITH_NO_ACCESS), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            authorizationService.Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<FileTransferEntity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            authorizationService.Setup(x => x.CheckAccessAsSenderOrRecipient(It.IsAny<ClaimsPrincipal?>(), It.IsAny<FileTransferEntity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            authorizationService.Setup(x => x.CheckAccessForSearch(It.IsAny<ClaimsPrincipal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(authorizationService.Object);

            var eventBus = new Mock<IEventBus>();
            services.AddSingleton(eventBus.Object);
            var sp = services.BuildServiceProvider();
            var _backgroundJobClient = sp.GetRequiredService<IBackgroundJobClient>();
            var policy = Policy.Handle<Exception>().WaitAndRetry(10, _ => TimeSpan.FromSeconds(1));
            var result = policy.ExecuteAndCapture(() => _backgroundJobClient.Enqueue(() => Console.WriteLine("Hello World!")));

            services.AddHangfire(services =>
            {
                services.UseMemoryStorage();
            });
            services.RemoveAll<IRecurringJobManager>();
            services.AddSingleton(new Mock<IRecurringJobManager>().Object);
            if (result.Outcome == OutcomeType.Failure)
            {
                throw new InvalidOperationException("Hangfire could not be installed");
            }
        });
    }

    public HttpClient CreateClientWithAuthorization(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }
}
