using Hangfire;

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
            HangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            services.AddSingleton(HangfireBackgroundJobClient.Object);
        });
    }
}
