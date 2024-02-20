using System.Net.Http.Headers;

using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Hangfire;
using Hangfire.MemoryStorage;

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
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(async options =>
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
            services.AddSingleton(HangfireBackgroundJobClient.Object);

            var authorizationService = new Mock<IResourceRightsRepository>();
            authorizationService.Setup(x => x.CheckUserAccess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ResourceAccessLevel>(), It.IsAny<bool>())).ReturnsAsync(true);
            services.AddSingleton(authorizationService.Object);
        });
    }

    public HttpClient CreateClientWithAuthorization(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
