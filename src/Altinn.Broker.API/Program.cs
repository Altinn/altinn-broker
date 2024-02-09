using System.Text.Json.Serialization;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Models;
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

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        var altinnOptions = new AltinnOptions();
        config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
        options.SaveToken = true;
        options.MetadataAddress = altinnOptions.OpenIdWellKnown;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationConstants.ResourceOwner, policy => policy.RequireClaim("scope", [ AuthorizationConstants.AdminScope ]));
        options.AddPolicy(AuthorizationConstants.Sender, policy => policy.RequireClaim("scope", [ AuthorizationConstants.SenderScope ]));
        options.AddPolicy(AuthorizationConstants.Recipient, policy => policy.RequireClaim("scope", [ AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope ] ));
        options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy => policy.RequireClaim("scope", [ AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope ]));
        options.AddPolicy(AuthorizationConstants.Legacy, policy => policy.RequireClaim("scope", [ AuthorizationConstants.Legacy ]));
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
