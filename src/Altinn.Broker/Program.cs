using System.Text.Json.Serialization;

using Altinn.Broker.Application;
using Altinn.Broker.Integrations;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Integrations.Hangfire;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models.Maskinporten;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;

using Hangfire;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Serilog;

// Using two-stage initialization to catch startup errors.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(
        TelemetryConfiguration.CreateDefault(),
        TelemetryConverter.Traces)
    .CreateLogger();

try
{
    BuildAndRun(args);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void BuildAndRun(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Fatal)
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces));

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);
    ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

    var app = builder.Build();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseAuthorization();

    app.MapControllers();

    app.UseHangfireDashboard();

    app.Run();
}

static void ConfigureServices(IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
{
    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddApplicationInsightsTelemetry();

    services.AddApplicationHandlers();
    services.AddIntegrations();
    services.AddPersistence();

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<AzureResourceManagerOptions>(config.GetSection(key: nameof(AzureResourceManagerOptions)));
    services.Configure<MaskinportenOptions>(config.GetSection(key: nameof(MaskinportenOptions)));

    services.AddHttpClient();
    services.AddProblemDetails();

    services.ConfigureHangfire();

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        var maskinportenOptions = new MaskinportenOptions();
        config.GetSection(nameof(MaskinportenOptions)).Bind(maskinportenOptions);
        options.SaveToken = true;
        options.MetadataAddress = $"{maskinportenOptions.Issuer}.well-known/oauth-authorization-server";
        if (hostEnvironment.IsDevelopment())
        {
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
        }
        else
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = maskinportenOptions.Issuer,
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        }
    });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("Sender", policy => policy.RequireClaim("scope", ["altinn:broker.write", "altinn:broker.write altinn:broker.read"]));
        options.AddPolicy("Recipient", policy => policy.RequireClaim("scope", ["altinn:broker.read", "altinn:broker.write altinn:broker.read"]));
        options.AddPolicy("SenderOrRecipient", policy => policy.RequireClaim("scope", ["altinn:broker.read", "altinn:broker.write", "altinn:broker.write altinn:broker.read"]));
        options.AddPolicy("Legacy", policy => policy.RequireClaim("scope", ["altinn:broker.legacy"]));
    });

    services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = null;
    });
    services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = long.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });
}

public partial class Program { } // For compatibility with WebApplicationFactory
