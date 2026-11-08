namespace HRMS.Application.Abstractions.Security;

/// <summary>Reads global user identities and validates their active tenant membership.</summary>
public interface IUserIdentityRepository
{
    Task<UserLoginRecord?> FindForLoginAsync(
        string normalizedEmail,
        string normalizedTenantCode,
        CancellationToken cancellationToken = default);

    Task RecordFailedLoginAsync(
        Guid userId,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default);

    Task RecordSuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdatePasswordHashAsync(
        Guid userId,
        string passwordHash,
        CancellationToken cancellationToken = default);
}
