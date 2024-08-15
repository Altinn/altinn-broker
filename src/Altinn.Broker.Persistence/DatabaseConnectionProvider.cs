using System.IdentityModel.Tokens.Jwt;

using Altinn.Broker.Persistence.Options;

using Azure.Core;
using Azure.Identity;

using Hangfire.PostgreSql;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

namespace Altinn.Broker.Persistence;

public class DatabaseConnectionProvider : IDisposable, IConnectionFactory
{
    private readonly string _connectionString;
    private NpgsqlDataSource _dataSource;
    private string? _accessToken;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly ILogger<DatabaseConnectionProvider> _logger;

    public DatabaseConnectionProvider(IOptions<DatabaseOptions> databaseOptions, ILogger<DatabaseConnectionProvider> logger)
    {
        _connectionString = databaseOptions.Value.ConnectionString ?? throw new ArgumentNullException("DatabaseOptions__ConnectionString");
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        var dataSource = dataSourceBuilder.Build();
        _dataSource = dataSource;
        _logger = logger;
    }


    public async Task<NpgsqlConnection> GetConnectionAsync()
    {
        await EnsureValidDataSource();
        return await _dataSource.OpenConnectionAsync();
    }

    public async Task<NpgsqlCommand> CreateCommand(string commandText)
    {
        await EnsureValidDataSource();
        return _dataSource.CreateCommand(commandText);
    }

    public NpgsqlConnection GetOrCreateConnection()
    {
        return GetConnectionAsync().Result;
    }
    private async Task EnsureValidDataSource()
    {
        await _semaphore.WaitAsync();
        try
        {
            NpgsqlConnectionStringBuilder connectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString);
            if (!string.IsNullOrWhiteSpace(connectionStringBuilder.Password)) // Developer mode
            {
                return;
            }
            if (_dataSource is not null && IsAccessTokenValid())
            {
                return;
            }
            await RefreshToken();
            connectionStringBuilder.Password = _accessToken;
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);
            _dataSource = dataSourceBuilder.Build();
            return;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RefreshToken()
    {
        _logger.LogInformation($"New token being prepared. Previous token was {(string.IsNullOrWhiteSpace(_accessToken) ? "not set" : "expired")}.");
        var sqlServerTokenProvider = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
        _accessToken = (await sqlServerTokenProvider.GetTokenAsync(
            new TokenRequestContext(scopes: ["https://ossrdbms-aad.database.windows.net/.default"]) { })).Token;
    }

    private bool IsAccessTokenValid()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return false;
        }
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.ReadToken(_accessToken);
        return DateTime.UtcNow.AddSeconds(60) < token.ValidTo;
    }

    public void Dispose()
    {
        _dataSource?.Dispose();
    }
}
