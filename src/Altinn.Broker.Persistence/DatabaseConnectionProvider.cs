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
    private NpgsqlConnection? _connection;
    private string? _accessToken;

    public DatabaseConnectionProvider(IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionString = databaseOptions.Value.ConnectionString ?? throw new ArgumentNullException("DatabaseOptions__ConnectionString");
    }

    public async Task<NpgsqlConnection> GetConnectionAsync()
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString);
        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Password))
        {
            connectionStringBuilder.Password = _accessToken;
            if (!IsAccessTokenValid())
            {
                await RefreshToken();
                connectionStringBuilder.Password = _accessToken;
                _connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);
            }
        }

        if (_connection is null)
        {
            _connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);
            _connection.Open();
        }
        else if (_connection.State == System.Data.ConnectionState.Closed)
        {
            _connection.Open();
        }
        else if (_connection.State == System.Data.ConnectionState.Broken)
        {
            _connection.Close();
            _connection.Open();
        }

        return _connection;
    }

    private async Task RefreshToken()
    {
        var sqlServerTokenProvider = new DefaultAzureCredential();
        _accessToken = (await sqlServerTokenProvider.GetTokenAsync(
            new TokenRequestContext(scopes: new string[] { "https://ossrdbms-aad.database.windows.net/.default" }) { })).Token;        
    }

    private bool IsAccessTokenValid()
    {        
        if (string.IsNullOrWhiteSpace(_accessToken)) {
            return false;
        }
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.ReadToken(_accessToken);        
        return token.ValidTo > DateTime.Now.Subtract(TimeSpan.FromSeconds(60));
    }

    private void CloseConnection()
    {
        if (_connection != null && _connection.State != System.Data.ConnectionState.Closed)
        {
            _connection.Close();
        }
    }

    public void Dispose()
    {
        CloseConnection();
        _connection?.Dispose();
    }
}
