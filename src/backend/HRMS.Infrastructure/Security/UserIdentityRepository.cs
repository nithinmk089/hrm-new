using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;

namespace HRMS.Infrastructure.Security;

/// <summary>Persists authentication state against the canonical security schema.</summary>
internal sealed class UserIdentityRepository(IDbConnectionFactory connectionFactory)
    : IUserIdentityRepository
{
    public async Task<UserLoginRecord?> FindForLoginAsync(
        string normalizedEmail,
        string normalizedTenantCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTenantCode);

        const string sql = """
            SELECT
                u.UserId,
                u.DisplayName,
                u.PasswordHash,
                u.IsEnabled,
                u.FailedAccessCount,
                u.LockoutEndUtc,
                t.TenantId,
                t.TenantCode,
                t.TenantName,
                m.MembershipId
            FROM security.Users AS u
            INNER JOIN security.UserTenantMemberships AS m
                ON m.UserId = u.UserId
               AND m.MembershipStatus = 'Active'
            INNER JOIN platform.Tenants AS t
                ON t.TenantId = m.TenantId
               AND t.Status = 'Active'
               AND t.IsDeleted = 0
            WHERE u.NormalizedEmail = @NormalizedEmail
              AND UPPER(LTRIM(RTRIM(t.TenantCode))) = @NormalizedTenantCode
              AND u.IsDeleted = 0;
            """;

        await using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<UserLoginRecord>(
            new CommandDefinition(
                sql,
                new { NormalizedEmail = normalizedEmail, NormalizedTenantCode = normalizedTenantCode },
                cancellationToken: cancellationToken));
    }

    public async Task RecordFailedLoginAsync(
        Guid userId,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFailedAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lockoutDuration);
        EnsureValidUserId(userId);

        const string sql = """
            UPDATE security.Users
            SET
                FailedAccessCount = FailedAccessCount + 1,
                LockoutEndUtc = CASE
                    WHEN FailedAccessCount + 1 >= @MaxFailedAttempts
                        THEN DATEADD(SECOND, @LockoutSeconds, SYSUTCDATETIME())
                    ELSE LockoutEndUtc
                END,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE UserId = @UserId
              AND IsEnabled = 1
              AND IsDeleted = 0;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    UserId = userId,
                    MaxFailedAttempts = maxFailedAttempts,
                    LockoutSeconds = checked((int)lockoutDuration.TotalSeconds)
                },
                cancellationToken: cancellationToken));
    }

    public async Task RecordSuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureValidUserId(userId);

        const string sql = """
            UPDATE security.Users
            SET
                FailedAccessCount = 0,
                LockoutEndUtc = NULL,
                LastLoginAtUtc = SYSUTCDATETIME(),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE UserId = @UserId
              AND IsEnabled = 1
              AND IsDeleted = 0;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }

    private static void EnsureValidUserId(Guid userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
    }
}
