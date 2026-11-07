using System.Data.Common;
using HRMS.Application.Abstractions.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Persistence;

internal sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Hrms")
        ?? throw new InvalidOperationException(
            "Connection string 'Hrms' is missing. Configure it using .NET User Secrets or environment configuration.");

    public DbConnection CreateConnection() => new SqlConnection(_connectionString);
}
