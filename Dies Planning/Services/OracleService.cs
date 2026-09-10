using Oracle.ManagedDataAccess.Client;

namespace Dies_Planning.Services
{
    public class OracleService
    {
        private readonly string _connectionString;

        public OracleService(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("OracleDb")
                ?? throw new InvalidOperationException(
                    "OracleDb connection string is not configured.");
        }

        public async Task<bool> TestConnectionAsync()
        {
            await using var connection =
                new OracleConnection(_connectionString);

            await connection.OpenAsync();

            return connection.State == System.Data.ConnectionState.Open;
        }
    }
}

