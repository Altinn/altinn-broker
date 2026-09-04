using System.Reflection;
using System.Text.Json.Serialization;

using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Broker.API.AltinnPlatformAuth;
using Altinn.Broker.API.Authentication;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Filters;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.API.IdPortenDirectAuth;
using Altinn.Broker.API.Swagger;
using Altinn.Broker.API.Tus;
using Altinn.Broker.Application;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Helpers;
using Altinn.Broker.Integrations;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Integrations.Hangfire;
using Altinn.Broker.Integrations.Tus;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;
using Altinn.Common.PEP.Authorization;

using Hangfire;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using StackExchange.Redis;

BuildAndRun(args);

static ILogger<Program> CreateBootstrapLogger()
{
    return LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Altinn.Broker.API.Program", LogLevel.Debug)
            .AddConsole();
    }).CreateLogger<Program>();
}

static void BuildAndRun(string[] args)
{
    var bootstrapLogger = CreateBootstrapLogger();
    bootstrapLogger.LogInformation("Starting Altinn.Broker.API...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    ConfigureServices(builder.Services, builder.Configuration, builder.Environment);
    var generalSettings = builder.Configuration.GetSection(nameof(GeneralSettings)).Get<GeneralSettings>();
    bootstrapLogger.LogInformation($"Running in environment {builder.Environment.EnvironmentName}");
    builder.Services.ConfigureOpenTelemetry(generalSettings?.ApplicationInsightsConnectionString ?? string.Empty);

    var app = builder.Build();

    // Vite (and other reverse proxies) set X-Forwarded-* so OIDC/cookies see the SPA host.
    app.UseForwardedHeaders();

    app.UseMiddleware<TusPartialPathRewriteMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<AcceptHeaderValidationMiddleware>();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<CsrfProtectionMiddleware>();
    app.UseMiddleware<TusFileTransferIdRouteMiddleware>();
    app.UseMiddleware<TusIdempotentCompletePatchMiddleware>();

    app.MapControllers();
    app.MapBrokerTusUploads();

    app.UseHangfireDashboard();

    RecurringJobRegistration.Register(app.Services, app.Configuration, app.Logger);

    app.Run();
}

static void ConfigureServices(IServiceCollection services, IConfiguration config, IHostEnvironment hostEnvironment)
{
    services.AddHttpContextAccessor();
    services.AddSingleton(TimeProvider.System);
    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
        options.OperationFilter<BinaryRequestBodyOperationFilter>();
        options.DocumentFilter<TusUploadDocumentFilter>();
    });

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<AzureResourceManagerOptions>(config.GetSection(key: nameof(AzureResourceManagerOptions)));
    services.Configure<AltinnOptions>(config.GetSection(key: nameof(AltinnOptions)));
    services.Configure<MaskinportenSettings>(config.GetSection(key: nameof(MaskinportenSettings)));
    services.Configure<MaskinportenJwkRotationSettings>(config.GetSection(key: nameof(MaskinportenJwkRotationSettings)));
    services.AddSingleton<IValidateOptions<AzureStorageOptions>, AzureStorageOptionsValidator>();
    services.AddOptions<AzureStorageOptions>()
        .Bind(config.GetSection(key: nameof(AzureStorageOptions)))
        .ValidateOnStart();
    services.Configure<ReportStorageOptions>(config.GetSection(key: nameof(ReportStorageOptions)));
    services.Configure<ReportFilterOptions>(config);
    services.Configure<GeneralSettings>(config.GetSection(key: nameof(GeneralSettings)));
    services.Configure<DistributedCacheOptions>(config.GetSection(DistributedCacheOptions.SectionName));
    services.Configure<TusOptions>(config.GetSection(TusOptions.SectionName));

    services.AddApplicationHandlers();
    services.AddIntegrations(config, hostEnvironment.IsDevelopment());
    services.AddPersistence(config);

    services.AddHttpClient();
    services.AddProblemDetails();

    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto
            | ForwardedHeaders.XForwardedHost;
        // Only trust loopback for local Vite proxy. In deployed environments keep the
        // default KnownNetworks/KnownProxies so arbitrary clients cannot spoof X-Forwarded-*.
        if (hostEnvironment.IsDevelopment())
        {
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        }
    });

    // Shared Redis for distributed cache, TUS, and ASP.NET Data Protection keys
    // (OIDC state/correlation cookies must decrypt on any Container App replica).
    var distributedCacheOptions = config.GetSection(DistributedCacheOptions.SectionName).Get<DistributedCacheOptions>();
    if (!string.IsNullOrWhiteSpace(distributedCacheOptions?.RedisConnectionString))
    {
        var redis = ConnectionMultiplexer.Connect(distributedCacheOptions.RedisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(redis);
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = distributedCacheOptions.RedisConnectionString;
        });
        services.AddDataProtection()
            .SetApplicationName("Altinn.Broker.API")
            .PersistKeysToStackExchangeRedis(redis, "Altinn.Broker.API-DataProtection-Keys");
    }
    else
    {
        services.AddDistributedMemoryCache();
    }
    services.AddHybridCache();

    services.AddHybridCache(options =>
    {
        options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(24),
            LocalCacheExpiration = TimeSpan.FromMinutes(5)
        };
    });

    // Register filters
    services.AddScoped<StatisticsApiKeyFilter>();

    services.ConfigureHangfire();

    services.AddAltinnPlatformAuth(config);

    services.AddAuthentication()
        .AddScheme<AuthenticationSchemeOptions, TusUploadSessionAuthenticationHandler>(
            AuthorizationConstants.TusUploadSession,
            _ => { })
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
                ValidateLifetime = !hostEnvironment.IsDevelopment(),
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents()
            {
                OnAuthenticationFailed = AltinnTokenEventsHelper.OnAuthenticationFailed,
                OnChallenge = AltinnTokenEventsHelper.OnChallenge
            };
        })
        .AddJwtBearer(AuthorizationConstants.LegacyAndMaskinporten, options =>
        {
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
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = AltinnTokenEventsHelper.OnAuthenticationFailed,
                OnChallenge = AltinnTokenEventsHelper.OnChallenge
            };
        })
        .AddAltinnPlatformJwtCookie()
        .AddIdPortenDirectAuth(config);

    services.AddScoped<TusUploadSessionAuthenticationHelper>();

    services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
    services.AddTransient<IAuthorizationHandler, EndUserAuthorizationHandler>();
    services.AddTransient<IAuthorizationHandler, EndUserScopeAccessHandler>();
    services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationConstants.Sender, policy => policy
            .AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.SenderScope))
            .AddAuthenticationSchemes(
                AuthorizationConstants.TusUploadSession,
                JwtBearerDefaults.AuthenticationScheme,
                AuthorizationConstants.LegacyAndMaskinporten,
                AuthorizationConstants.EndUserCookie,
                AuthorizationConstants.AltinnPlatformJwtCookie));
        options.AddPolicy(AuthorizationConstants.Recipient, policy => policy
            .AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.RecipientScope))
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                AuthorizationConstants.LegacyAndMaskinporten,
                AuthorizationConstants.EndUserCookie,
                AuthorizationConstants.AltinnPlatformJwtCookie));
        options.AddPolicy(AuthorizationConstants.SenderOrRecipient, policy => policy
            .AddRequirements(new ScopeAccessRequirement(
                [AuthorizationConstants.SenderScope, AuthorizationConstants.RecipientScope]))
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                AuthorizationConstants.LegacyAndMaskinporten,
                AuthorizationConstants.EndUserCookie,
                AuthorizationConstants.AltinnPlatformJwtCookie));
        options.AddPolicy(AuthorizationConstants.ServiceOwner, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.ServiceOwnerScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.LegacyAndMaskinporten));
        options.AddPolicy(AuthorizationConstants.Maintenance, policy => policy.AddRequirements(new ScopeAccessRequirement(AuthorizationConstants.MaintenanceScope)).AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AuthorizationConstants.LegacyAndMaskinporten));
        options.AddPolicy(AuthorizationConstants.EndUser, policy =>
        {
            policy.AddAuthenticationSchemes(
                AuthorizationConstants.EndUserCookie,
                AuthorizationConstants.AltinnPlatformJwtCookie);
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new EndUserRequirement());
        });
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
