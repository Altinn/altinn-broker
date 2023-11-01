using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;
using Altinn.Broker.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    services.AddSingleton<IFileStore, BlobStore>();

    services.Configure<DatabaseOptions>(config.GetSection(key: nameof(DatabaseOptions)));
    services.Configure<StorageOptions>(config.GetSection(key: nameof(StorageOptions)));
    services.AddSingleton<DatabaseConnectionProvider>();

    services.AddSingleton<IActorRepository, ActorRepository>();
    services.AddSingleton<IFileRepository, FileRepository>();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();