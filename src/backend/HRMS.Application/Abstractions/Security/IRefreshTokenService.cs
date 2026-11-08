namespace HRMS.Application.Abstractions.Security;

/// <summary>Coordinates refresh-token creation, rotation and revocation.</summary>
public interface IRefreshTokenService
{
    Task<RefreshSessionResult?> CreateSessionAsync(
        UserLoginRecord user,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRefreshResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string refreshToken,
        string? ipAddress,
        string reason = "Logout",
        CancellationToken cancellationToken = default);
}
