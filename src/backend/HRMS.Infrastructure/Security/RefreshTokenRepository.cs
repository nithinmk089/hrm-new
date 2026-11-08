using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;

namespace HRMS.Infrastructure.Security;

/// <summary>
/// SQL Server persistence for refresh tokens. Rotation and reuse detection are serialized
/// by an update lock on the unique token-hash row and committed as one transaction.
/// </summary>
internal sealed class RefreshTokenRepository(IDbConnectionFactory connectionFactory)
    : IRefreshTokenRepository
{
    public async Task<bool> CreateAsync(
        UserLoginRecord user,
        Guid refreshTokenId,
        byte[] tokenHash,
        DateTime expiresAtUtc,
        string? createdByIp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateToken(refreshTokenId, tokenHash);
        ValidateIp(createdByIp);

        const string sql = """
            INSERT INTO security.RefreshTokens
            (
                RefreshTokenId, TenantId, UserId, MembershipId, TokenHash,
                TokenFamilyId, ExpiresAtUtc, CreatedByIp
            )
            SELECT
                @RefreshTokenId, @TenantId, @UserId, @MembershipId, @TokenHash,
                @RefreshTokenId, @ExpiresAtUtc, @CreatedByIp
            WHERE EXISTS
            (
                SELECT 1
                FROM security.Users AS u
                INNER JOIN security.UserTenantMemberships AS m
                    ON m.UserId = u.UserId
                   AND m.TenantId = @TenantId
                   AND m.MembershipId = @MembershipId
                   AND m.MembershipStatus = 'Active'
                INNER JOIN platform.Tenants AS t
                    ON t.TenantId = m.TenantId
                   AND t.Status = 'Active'
                   AND t.IsDeleted = 0
                WHERE u.UserId = @UserId
                  AND u.IsEnabled = 1
                  AND u.IsDeleted = 0
            );
            """;

        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RefreshTokenId = refreshTokenId,
                user.TenantId,
                user.UserId,
                user.MembershipId,
                TokenHash = tokenHash,
                ExpiresAtUtc = expiresAtUtc,
                CreatedByIp = createdByIp
            },
            cancellationToken: cancellationToken));

        return affected == 1;
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(
        byte[] currentTokenHash,
        Guid replacementTokenId,
        byte[] replacementTokenHash,
        DateTime nowUtc,
        DateTime replacementExpiresAtUtc,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(currentTokenHash, nameof(currentTokenHash));
        ValidateToken(replacementTokenId, replacementTokenHash);
        ValidateIp(ipAddress);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        const string selectSql = """
            SELECT
                rt.RefreshTokenId,
                rt.TenantId,
                rt.UserId,
                rt.MembershipId,
                rt.TokenFamilyId,
                rt.ExpiresAtUtc,
                rt.RevokedAtUtc,
                rt.ReplacedByTokenId,
                u.DisplayName,
                u.Email,
                u.PasswordHash,
                u.IsEnabled,
                u.FailedAccessCount,
                u.LockoutEndUtc,
                t.TenantCode,
                t.TenantName,
                CASE
                    WHEN u.UserId IS NOT NULL
                     AND u.IsEnabled = 1
                     AND u.IsDeleted = 0
                     AND m.MembershipStatus = 'Active'
                     AND t.Status = 'Active'
                     AND t.IsDeleted = 0
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS IsIdentityActive
            FROM security.RefreshTokens AS rt WITH (UPDLOCK, HOLDLOCK)
            LEFT JOIN security.Users AS u
                ON u.UserId = rt.UserId
            LEFT JOIN security.UserTenantMemberships AS m
                ON m.MembershipId = rt.MembershipId
               AND m.TenantId = rt.TenantId
               AND m.UserId = rt.UserId
            LEFT JOIN platform.Tenants AS t
                ON t.TenantId = rt.TenantId
            WHERE rt.TokenHash = @TokenHash;
            """;

        var token = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(
            new CommandDefinition(
                selectSql,
                new { TokenHash = currentTokenHash },
                transaction,
                cancellationToken: cancellationToken));

        if (token is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RefreshTokenRotationResult(RefreshTokenRotationStatus.Invalid, null);
        }

        if (token.RevokedAtUtc is not null || token.ReplacedByTokenId is not null)
        {
            const string revokeFamilySql = """
                UPDATE security.RefreshTokens
                SET RevokedAtUtc = COALESCE(RevokedAtUtc, @NowUtc),
                    RevokedByIp = COALESCE(@IpAddress, RevokedByIp),
                    RevocationReason = N'ReuseDetected'
                WHERE TenantId = @TenantId
                  AND UserId = @UserId
                  AND TokenFamilyId = @TokenFamilyId
                  AND RevokedAtUtc IS NULL;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                revokeFamilySql,
                new
                {
                    NowUtc = nowUtc,
                    IpAddress = ipAddress,
                    token.TenantId,
                    token.UserId,
                    token.TokenFamilyId
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new RefreshTokenRotationResult(RefreshTokenRotationStatus.ReuseDetected, null);
        }

        if (token.ExpiresAtUtc <= nowUtc || !token.IsIdentityActive)
        {
            const string revokeSql = """
                UPDATE security.RefreshTokens
                SET RevokedAtUtc = @NowUtc,
                    RevokedByIp = @IpAddress,
                    RevocationReason = @Reason
                WHERE RefreshTokenId = @RefreshTokenId
                  AND RevokedAtUtc IS NULL;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                revokeSql,
                new
                {
                    NowUtc = nowUtc,
                    IpAddress = ipAddress,
                    Reason = token.ExpiresAtUtc <= nowUtc ? "Expired" : "MembershipInactive",
                    token.RefreshTokenId
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new RefreshTokenRotationResult(RefreshTokenRotationStatus.Invalid, null);
        }

        const string replaceSql = """
            UPDATE security.RefreshTokens
            SET RevokedAtUtc = @NowUtc,
                RevokedByIp = @IpAddress,
                RevocationReason = N'Rotated',
                ReplacedByTokenId = @ReplacementTokenId
            WHERE RefreshTokenId = @CurrentTokenId
              AND RevokedAtUtc IS NULL
              AND ReplacedByTokenId IS NULL;

            IF @@ROWCOUNT <> 1
                THROW 51070, 'Refresh token rotation lost its concurrency guard.', 1;

            INSERT INTO security.RefreshTokens
            (
                RefreshTokenId, TenantId, UserId, MembershipId, TokenHash,
                TokenFamilyId, ExpiresAtUtc, CreatedByIp
            )
            VALUES
            (
                @ReplacementTokenId, @TenantId, @UserId, @MembershipId, @ReplacementTokenHash,
                @TokenFamilyId, @ReplacementExpiresAtUtc, @IpAddress
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            replaceSql,
            new
            {
                NowUtc = nowUtc,
                IpAddress = ipAddress,
                ReplacementTokenId = replacementTokenId,
                CurrentTokenId = token.RefreshTokenId,
                token.TenantId,
                token.UserId,
                token.MembershipId,
                token.TokenFamilyId,
                ReplacementTokenHash = replacementTokenHash,
                ReplacementExpiresAtUtc = replacementExpiresAtUtc
            },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        var user = new UserLoginRecord(
            token.UserId,
            token.DisplayName!,
            token.Email!,
            token.PasswordHash!,
            token.IsEnabled,
            token.FailedAccessCount,
            token.LockoutEndUtc,
            token.TenantId,
            token.TenantCode!,
            token.TenantName!,
            token.MembershipId);

        return new RefreshTokenRotationResult(RefreshTokenRotationStatus.Rotated, user);
    }

    public async Task<bool> RevokeAsync(
        byte[] tokenHash,
        DateTime revokedAtUtc,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(tokenHash, nameof(tokenHash));
        ValidateIp(ipAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        const string sql = """
            UPDATE security.RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc,
                RevokedByIp = @IpAddress,
                RevocationReason = @Reason
            WHERE TokenHash = @TokenHash
              AND RevokedAtUtc IS NULL
              AND ReplacedByTokenId IS NULL;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RevokedAtUtc = revokedAtUtc,
                IpAddress = ipAddress,
                Reason = reason,
                TokenHash = tokenHash
            },
            cancellationToken: cancellationToken));

        return affected == 1;
    }

    private static void ValidateToken(Guid tokenId, byte[] tokenHash)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tokenId, Guid.Empty);
        ValidateHash(tokenHash, nameof(tokenHash));
    }

    private static void ValidateHash(byte[] hash, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(hash, parameterName);
        if (hash.Length != 32)
        {
            throw new ArgumentException("A refresh-token SHA-256 hash must contain exactly 32 bytes.", parameterName);
        }
    }

    private static void ValidateIp(string? ipAddress)
    {
        if (ipAddress is { Length: > 45 })
        {
            throw new ArgumentOutOfRangeException(nameof(ipAddress), "IP address cannot exceed 45 characters.");
        }
    }

    private sealed class RefreshTokenRow
    {
        public Guid RefreshTokenId { get; init; }
        public Guid TenantId { get; init; }
        public Guid UserId { get; init; }
        public Guid MembershipId { get; init; }
        public Guid TokenFamilyId { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public DateTime? RevokedAtUtc { get; init; }
        public Guid? ReplacedByTokenId { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
        public string? PasswordHash { get; init; }
        public bool IsEnabled { get; init; }
        public int FailedAccessCount { get; init; }
        public DateTime? LockoutEndUtc { get; init; }
        public string? TenantCode { get; init; }
        public string? TenantName { get; init; }
        public bool IsIdentityActive { get; init; }
    }
}
