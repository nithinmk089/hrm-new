namespace HRMS.Application.Abstractions.Security;

/// <summary>Safe login response. Never includes password hashes or refresh-token material.</summary>
public sealed record LoginResult(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string DisplayName,
    Guid TenantId,
    string TenantCode,
    string TenantName);
