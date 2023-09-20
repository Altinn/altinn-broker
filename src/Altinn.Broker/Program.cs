using Altinn.Broker.Core.Extensions;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence;
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
    services.AddSingleton<IFileStore, FileStore>();
    services.AddSingleton<IShipmentRepository, ShipmentRepository>();
    services.AddSingleton<IActorRepository, ActorRepository>();
    services.AddSingleton<IFileRepository, FileRepository>();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();