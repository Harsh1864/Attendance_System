using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace project.Services
{
    /// <summary>
    /// Creates a new, opened connection per call. Npgsql pools connections,
    /// so this is cheap, and it avoids sharing one scoped connection between
    /// services (which breaks when two readers are open at once).
    /// </summary>
    public class DbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            var cs = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(cs))
                throw new System.InvalidOperationException(
                    "Connection string 'Default' is not configured. See README: Configure.");
            _connectionString = cs;
        }

        public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
        {
            var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            return conn;
        }
    }
}
