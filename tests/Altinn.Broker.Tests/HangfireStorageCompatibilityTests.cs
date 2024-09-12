using System.Transactions;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;
public class HangfireStorageCompatibilityTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly NpgsqlDataSource _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

    // TransactionScope is used to ensure eventual consistency for events posted to events API 
    // Test ensures that Hangfire implementation is compatible with TransactionScope.
    [Fact]
    public async void BackgroundJobClient_TransactionScopeCompatible()
    {
        var migrateConnection = await _dataSource.OpenConnectionAsync();
        PostgreSqlObjectsInstaller.Install(migrateConnection);
        var connectionFactory = new TestConnectionFactory(_dataSource);
        var jobStorage = new PostgreSqlStorage(connectionFactory);
        var backgroundJobClient = new BackgroundJobClient(jobStorage);
        long parentJobId;
        var outsideConnection = await _dataSource.OpenConnectionAsync();
        using (var transaction = new TransactionScope(TransactionScopeOption.Required))
        {
            var parentJob = backgroundJobClient.Enqueue(() => Console.WriteLine("Hello World!"));
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

    internal class TestConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection()
        {
            return dataSource.CreateConnection();
        }
    }   
}
