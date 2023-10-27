using Altinn.Broker.Core.Extensions;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Repositories.Interfaces;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;
using Altinn.Broker.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    services.AddCoreServices(config);
    services.AddSingleton<IDataService, DataStore>();
    services.AddSingleton<IFileStore, BlobStore>();
    services.AddSingleton<IFileStorage, FileStore>();

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<StorageOptions>(config.GetSection(key: nameof(StorageOptions)));
    services.AddSingleton<DatabaseConnectionProvider>();

    services.AddSingleton<IShipmentRepository, ShipmentRepository>();
    services.AddSingleton<IActorRepository, ActorRepository>();
    services.AddSingleton<IFileRepository, FileRepository>();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
