using HRMS.Application.Abstractions.Security;

namespace HRMS.Application.Security;

/// <summary>Validates credentials and creates a refresh-backed session for active tenant memberships.</summary>
public sealed class AuthenticationService(
    IUserIdentityRepository identityRepository,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider) : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginSessionResult?> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.TenantCode))
        {
            return null;
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var normalizedTenantCode = request.TenantCode.Trim().ToUpperInvariant();

        var user = await identityRepository.FindForLoginAsync(
            normalizedEmail, normalizedTenantCode, cancellationToken);

        if (user is null || !user.IsEnabled)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.LockoutEndUtc is { } lockoutEndUtc && lockoutEndUtc > now)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user.PasswordHash, request.Password);
        if (verification == PasswordVerificationOutcome.Failed)
        {
            await identityRepository.RecordFailedLoginAsync(
                user.UserId, MaxFailedAttempts, LockoutDuration, cancellationToken);
            return null;
        }

        if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            var upgradedHash = passwordHasher.HashPassword(request.Password);
            await identityRepository.UpdatePasswordHashAsync(user.UserId, upgradedHash, cancellationToken);
        }

        await identityRepository.RecordSuccessfulLoginAsync(user.UserId, cancellationToken);

        var session = await refreshTokenService.CreateSessionAsync(user, ipAddress, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var login = new LoginResult(
            session.AccessToken.Value,
            "Bearer",
            session.AccessToken.ExpiresAtUtc,
            user.UserId,
            user.DisplayName,
            user.TenantId,
            user.TenantCode,
            user.TenantName);

        return new LoginSessionResult(login, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
    }
}
