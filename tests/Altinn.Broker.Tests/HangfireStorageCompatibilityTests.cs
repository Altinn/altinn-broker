using System.Transactions;

using Hangfire;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;
public class HangfireStorageCompatibilityTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly NpgsqlDataSource _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
    private readonly IBackgroundJobClient _backgroundJobClient = factory.Services.GetRequiredService<IBackgroundJobClient>();

     // TransactionScope is used to ensure eventual consistency for events posted to events API 
     // Test ensures that Hangfire implementation is compatible with TransactionScope.
    [Fact]
    public async void BackgroundJobClient_TransactionScopeCompatible()
    {
        var jobStorage = JobStorage.Current;
        long parentJobId;
        var outsideConnection = await _dataSource.OpenConnectionAsync();
        using (var transaction = new TransactionScope(TransactionScopeOption.Required))
        {
            var parentJob = _backgroundJobClient.Enqueue(() => Console.WriteLine("Hello World!"));
            parentJobId = long.Parse(parentJob);
            var command = outsideConnection.CreateCommand();
            command.CommandText = "select COUNT(job) FROM hangfire.job WHERE id = @jobId";
            command.Parameters.AddWithValue("jobId", parentJobId);
            var result = command.ExecuteScalar();
            Assert.True((long)command.ExecuteScalar() == 0);
            transaction.Complete();
        }
        var postCommitCommand = _dataSource.CreateCommand("select COUNT(job) FROM hangfire.job WHERE id = @jobId");
        postCommitCommand.Parameters.AddWithValue("jobId", parentJobId);
        Assert.True((long)postCommitCommand.ExecuteScalar() == 1);
    }   

}
