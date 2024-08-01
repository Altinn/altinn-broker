using System.Text.Json.Serialization;

using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Helpers;
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
    app.Services.GetService<IRecurringJobManager>().AddOrUpdate<IdempotencyService>("Delete old impotency events", handler => handler.DeleteOldIdempotencyEvents(), Cron.Weekly());

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

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<AzureResourceManagerOptions>(config.GetSection(key: nameof(AzureResourceManagerOptions)));
    services.Configure<AltinnOptions>(config.GetSection(key: nameof(AltinnOptions)));
    services.Configure<MaskinportenSettings>(config.GetSection(key: nameof(MaskinportenSettings)));
    services.Configure<ApplicationSettings>(config.GetSection(key: nameof(ApplicationSettings)));

    services.AddApplicationHandlers();
    services.AddIntegrations(config, hostEnvironment.IsDevelopment());
    services.AddPersistence();

    services.AddHttpClient();
    services.AddProblemDetails();

    services.ConfigureHangfire();

    services.AddAuthentication()
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
            options.Events = new JwtBearerEvents()
            {
                OnAuthenticationFailed = context => JWTBearerEventsHelper.OnAuthenticationFailed(context),
                OnChallenge = c =>
                {
                    c.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        })
        .AddJwtBearer(AuthorizationConstants.Legacy, options => // To support "overgangslosningen"
        {
            var altinnOptions = new AltinnOptions();
            config.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);
            options.SaveToken = true;
            if (hostEnvironment.IsProduction())
            {
                options.MetadataAddress = "https://maskinporten.no/.well-known/oauth-authorization-server";
            }
            else
            {
                options.MetadataAddress = "https://test.maskinporten.no/.well-known/oauth-authorization-server";
            }
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = !hostEnvironment.IsDevelopment(),
                ClockSkew = TimeSpan.Zero
            };
        });

    services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
    services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationConstants.Sender, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.SenderScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
        options.AddPolicy(AuthorizationConstants.Recipient, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.RecipientScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
        options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy => policy.AddRequirements(new ScopeAccessRequirement([AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope])).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
        options.AddPolicy(AuthorizationConstants.Legacy, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.LegacyScope)).AddAuthenticationSchemes(AuthorizationConstants.Legacy));
        options.AddPolicy(AuthorizationConstants.ServiceOwner, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.ServiceOwnerScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
    });

    services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = null;
        options.Limits.MaxRequestBufferSize = null;
        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
        options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(10));
    });
    services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = long.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });
}

public partial class Program { } // For compatibility with WebApplicationFactory
