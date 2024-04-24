using Altinn.Broker.HangfireDashboard;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(
        c => c.UseNpgsqlConnection(builder.Configuration["AZURE_CONNECTION_STRING"])
    )
);
var app = builder.Build();

app.UseHangfireDashboard("/hangfire");
app.MapGet("/", () => Results.Redirect("/hangfire"));

app.Run();
