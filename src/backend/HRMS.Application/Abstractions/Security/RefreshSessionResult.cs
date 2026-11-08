namespace HRMS.Application.Abstractions.Security;

/// <summary>
/// Internal authentication session material. The raw refresh token must only be
/// delivered through a secure HttpOnly cookie and must never be logged.
/// </summary>
public sealed record RefreshSessionResult(
    UserLoginRecord User,
    GeneratedAccessToken AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
