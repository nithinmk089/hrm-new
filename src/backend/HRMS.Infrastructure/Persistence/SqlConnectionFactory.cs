using System.Data.Common;
using HRMS.Application.Abstractions.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Persistence;

internal sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = GetConnectionString(configuration);

    public DbConnection CreateConnection() => new SqlConnection(_connectionString);

    private static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Hrms");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Hrms' is missing or empty. Configure it using .NET User Secrets or environment configuration.");
        }

        return connectionString;
    }
}
