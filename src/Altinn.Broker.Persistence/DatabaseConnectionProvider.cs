using Altinn.Broker.Persistence.Options;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Altinn.Broker.Persistence;

public class DatabaseConnectionProvider : IDisposable
{
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;

    public DatabaseConnectionProvider(IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionString = databaseOptions.Value.ConnectionString ?? throw new ArgumentNullException("DatabaseOptions__ConnectionString");
    }

    public NpgsqlConnection GetConnection()
    {
        if (_connection is null)
        {
            _connection = new NpgsqlConnection(_connectionString);
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
