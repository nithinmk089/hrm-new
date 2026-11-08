namespace HRMS.Application.Abstractions.Security;

/// <summary>
/// Minimal identity and tenant-membership data required to validate a login.
/// Password hashes are internal application data and must never be returned by an API.
/// </summary>
public sealed record UserLoginRecord(
    Guid UserId,
    string DisplayName,
    string Email,
    string PasswordHash,
    bool IsEnabled,
    int FailedAccessCount,
    DateTime? LockoutEndUtc,
    Guid TenantId,
    string TenantCode,
    string TenantName,
    Guid MembershipId);
