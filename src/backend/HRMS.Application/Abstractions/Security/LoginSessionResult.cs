namespace HRMS.Application.Abstractions.Security;

/// <summary>Login response plus cookie-only refresh-token material for the API boundary.</summary>
public sealed record LoginSessionResult(
    LoginResult Login,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
