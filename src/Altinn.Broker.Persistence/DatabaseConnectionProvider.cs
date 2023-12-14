using System.IdentityModel.Tokens.Jwt;

using Altinn.Broker.Persistence.Options;

using Azure.Core;
using Azure.Identity;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

namespace Altinn.Broker.Persistence;

public class DatabaseConnectionProvider : IDisposable
{
    private readonly string _connectionString;
    private NpgsqlDataSource _dataSource;
    private string? _accessToken;

    public DatabaseConnectionProvider(IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionString = databaseOptions.Value.ConnectionString ?? throw new ArgumentNullException("DatabaseOptions__ConnectionString");
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        var dataSource = dataSourceBuilder.Build();
        _dataSource = dataSource;
    }

    public async Task<NpgsqlConnection> GetConnectionAsync()
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString);
        if (!string.IsNullOrWhiteSpace(connectionStringBuilder.Password)) // Developer mode
        {
            return await _dataSource.OpenConnectionAsync();
        }
        if (_dataSource is null || !IsAccessTokenValid())
        {
            await RefreshToken();
            connectionStringBuilder.Password = _accessToken;
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);
            _dataSource = dataSourceBuilder.Build();
        }
        return await _dataSource.OpenConnectionAsync();
    }

    private async Task RefreshToken()
    {
        var sqlServerTokenProvider = new DefaultAzureCredential();
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
        return token.ValidTo > DateTime.Now.Subtract(TimeSpan.FromSeconds(60));
    }

    public void Dispose()
    {
        _dataSource?.Dispose();
    }
}
