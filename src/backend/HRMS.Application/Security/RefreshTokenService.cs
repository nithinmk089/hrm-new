using System.Security.Cryptography;
using System.Text;
using HRMS.Application.Abstractions.Security;

namespace HRMS.Application.Security;

/// <summary>
/// Coordinates opaque refresh-token lifecycle and issues a new access token only
/// after the repository confirms an atomic successful rotation.
/// </summary>
public sealed class RefreshTokenService(
    IRefreshTokenGenerator refreshTokenGenerator,
    IRefreshTokenRepository refreshTokenRepository,
    IAccessTokenGenerator accessTokenGenerator,
    TimeProvider timeProvider,
    RefreshTokenSettings settings) : IRefreshTokenService
{
    public const int DefaultLifetimeDays = 14;

    public async Task<RefreshSessionResult?> CreateSessionAsync(
        UserLoginRecord user,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateSettings();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var generated = refreshTokenGenerator.Generate();
        var expiresAtUtc = now.AddDays(settings.LifetimeDays);

        var created = await refreshTokenRepository.CreateAsync(
            user,
            Guid.NewGuid(),
            generated.Hash,
            expiresAtUtc,
            ipAddress,
            cancellationToken);

        if (!created)
        {
            return null;
        }

        return new RefreshSessionResult(
            user,
            accessTokenGenerator.Generate(user),
            generated.Value,
            expiresAtUtc);
    }

    public async Task<RefreshTokenRefreshResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return new RefreshTokenRefreshResult(
                RefreshTokenRotationStatus.Invalid,
                null);
        }

        ValidateSettings();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var currentHash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        var replacement = refreshTokenGenerator.Generate();
        var replacementExpiresAtUtc = now.AddDays(settings.LifetimeDays);

        var rotation = await refreshTokenRepository.RotateAsync(
            currentHash,
            Guid.NewGuid(),
            replacement.Hash,
            now,
            replacementExpiresAtUtc,
            ipAddress,
            cancellationToken);

        if (rotation.Status != RefreshTokenRotationStatus.Rotated ||
            rotation.User is null)
        {
            return new RefreshTokenRefreshResult(rotation.Status, null);
        }

        var session = new RefreshSessionResult(
            rotation.User,
            accessTokenGenerator.Generate(rotation.User),
            replacement.Value,
            replacementExpiresAtUtc);

        return new RefreshTokenRefreshResult(
            RefreshTokenRotationStatus.Rotated,
            session);
    }

    public async Task<bool> RevokeAsync(
        string refreshToken,
        string? ipAddress,
        string reason = "Logout",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A revocation reason is required.", nameof(reason));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));

        return await refreshTokenRepository.RevokeAsync(
            hash,
            timeProvider.GetUtcNow().UtcDateTime,
            ipAddress,
            reason.Trim(),
            cancellationToken);
    }

    private void ValidateSettings()
    {
        if (settings.LifetimeDays is < 1 or > 90)
        {
            throw new InvalidOperationException(
                "Authentication:RefreshToken:LifetimeDays must be between 1 and 90.");
        }
    }
}
