using System.Text.Json.Serialization;

using Altinn.Broker.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models.Maskinporten;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;
using Altinn.Broker.Persistence.Repositories;
using Altinn.Broker.Repositories;

using Hangfire;
using Hangfire.MemoryStorage;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = long.MaxValue;
});
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthorization();

app.MapControllers();

app.Run();

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    services.AddApplicationHandlers();

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<AzureResourceManagerOptions>(config.GetSection(key: nameof(AzureResourceManagerOptions)));
    services.Configure<MaskinportenOptions>(config.GetSection(key: nameof(MaskinportenOptions)));
    services.AddSingleton<DatabaseConnectionProvider>();

    services.AddSingleton<IFileStore, BlobService>();
    services.AddSingleton<IActorRepository, ActorRepository>();
    services.AddSingleton<IFileRepository, FileRepository>();
    services.AddSingleton<IServiceOwnerRepository, ServiceOwnerRepository>();
    services.AddSingleton<IFileStore, BlobService>();
    services.AddSingleton<IBrokerStorageService, AzureBrokerStorageService>();
    services.AddSingleton<IResourceManager, AzureResourceManagerService>();

    services.AddHangfire(c => c.UseMemoryStorage());
    services.AddHangfireServer((options) =>
    {
        options.ServerTimeout = TimeSpan.FromMinutes(30);
    });

    services.AddHttpClient();

    if (app.Environment.IsDevelopment())
    {
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
    } else { 
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(async options =>
        {
            var maskinportenOptions = new MaskinportenOptions();
            config.GetSection(nameof(MaskinportenOptions)).Bind(maskinportenOptions);
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.MetadataAddress = $"{maskinportenOptions.Issuer}.well-known/oauth-authorization-server";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = maskinportenOptions.Issuer,
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        });
    }

    services.AddAuthorization(options =>
    {
        options.AddPolicy("Sender", policy => policy.RequireClaim("scope", ["altinn:broker.write"]));
        options.AddPolicy("Recipient", policy => policy.RequireClaim("scope", ["altinn:broker.read", "altinn:broker.write altinn:broker.read"]));
    });

    services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = int.MaxValue;
    });
    services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = int.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });
}

public partial class Program { } // For compatibility with WebApplicationFactory
