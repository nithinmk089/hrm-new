namespace HRMS.Application.Abstractions.Security;

/// <summary>Persists refresh-token hashes and performs single-use rotation atomically.</summary>
public interface IRefreshTokenRepository
{
    Task<bool> CreateAsync(
        UserLoginRecord user,
        Guid refreshTokenId,
        byte[] tokenHash,
        DateTime expiresAtUtc,
        string? createdByIp,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateAsync(
        byte[] currentTokenHash,
        Guid replacementTokenId,
        byte[] replacementTokenHash,
        DateTime nowUtc,
        DateTime replacementExpiresAtUtc,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        byte[] tokenHash,
        DateTime revokedAtUtc,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken = default);
}
