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
                [ErrorCode],
                [Message],
                [ExceptionDetails],
                [ApplicationName],
                [EnvironmentName]
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
                @StackTrace,
                @ApplicationName,
                @EnvironmentName
            );
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entry.TenantId,
                entry.UserId,
                entry.TraceId,
                entry.HttpMethod,
                entry.RequestPath,
                entry.StatusCode,
                ErrorType = entry.ErrorType,
                ErrorMessage = entry.ErrorMessage,
                StackTrace = entry.StackTrace,
                entry.ApplicationName,
                entry.EnvironmentName
            },
            commandTimeout: 5,
            cancellationToken: cancellationToken));
    }
}
