using System.Text.Json.Serialization;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Integrations;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Integrations.Hangfire;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;
using Altinn.Common.PEP.Authorization;

using Hangfire;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
    services.Configure<AltinnOptions>(config.GetSection(key: nameof(AltinnOptions)));

    services.AddHttpClient();
    services.AddProblemDetails();

    services.ConfigureHangfire();

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            options.SaveToken = true;
            options.MetadataAddress = altinnOptions.OpenIdWellKnown;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = !hostEnvironment.IsDevelopment(), // Do not validate lifetime in tests
                ClockSkew = TimeSpan.Zero
            };
        });

    services.AddAuthentication(AuthorizationConstants.Legacy)
        .AddJwtBearer(AuthorizationConstants.Legacy, options => { // To support "overgangslosningen"
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            options.SaveToken = true;
            options.MetadataAddress = "https://test.maskinporten.no/.well-known/oauth-authorization-server";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = !hostEnvironment.IsDevelopment(), // Do not validate lifetime in tests
                ClockSkew = TimeSpan.Zero
            };
        });

    services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
    services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationConstants.Sender, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.SenderScope)));
        options.AddPolicy(AuthorizationConstants.Recipient, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.RecipientScope)));
        options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy => policy.AddRequirements(new ScopeAccessRequirement([AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope])));
        options.AddPolicy(AuthorizationConstants.Legacy, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.LegacyScope)));
        options.AddPolicy(AuthorizationConstants.ResourceOwner, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.ResourceOwnerScope)));
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
