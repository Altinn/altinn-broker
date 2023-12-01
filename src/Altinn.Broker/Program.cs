using System.Text.Json.Serialization;

using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Middlewares;
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
    services.AddSingleton<IFileStore, BlobService>();

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<AzureStorageOptions>(config.GetSection(key: nameof(AzureStorageOptions)));
    services.Configure<AzureResourceManagerOptions>(config.GetSection(key: nameof(AzureResourceManagerOptions)));
    services.AddSingleton<DatabaseConnectionProvider>();

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

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "https://test.maskinporten.no/",
            //ValidAudience = "altinn-broker-sender",
            //IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateIssuerSigningKey = false,
            ValidateLifetime = false,
            RequireExpirationTime = false,
            RequireSignedTokens = false,
            SignatureValidator = delegate (string token, TokenValidationParameters parameters)
            {
                var jwt = new JsonWebToken(token);

                return jwt;
            },
        };
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
