using Dapper;
using HRMS.Application.Abstractions.Persistence;

namespace HRMS.Infrastructure.Persistence;

internal sealed class ApiErrorLogRepository(IDbConnectionFactory connectionFactory)
    : IApiErrorLogRepository
{
    public async Task InsertAsync(
        ApiErrorLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        const string sql = """
            INSERT INTO [audit].[ErrorLogs]
            (
                [TenantId],
                [UserId],
                [TraceId],
                [HttpMethod],
                [RequestPath],
                [StatusCode],
                [ErrorType],
                [ErrorMessage],
                [StackTrace]
            )
            VALUES
            (
                @TenantId,
                @UserId,
                @TraceId,
                @HttpMethod,
                @RequestPath,
                @StatusCode,
                @ErrorType,
                @ErrorMessage,
                @StackTrace
            );
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            entry,
            commandTimeout: 5,
            cancellationToken: cancellationToken));
    }
}
