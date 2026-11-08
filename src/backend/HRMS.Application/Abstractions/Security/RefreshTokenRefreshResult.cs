namespace HRMS.Application.Abstractions.Security;

/// <summary>Outcome of a refresh request and its newly issued session, when successful.</summary>
public sealed record RefreshTokenRefreshResult(
    RefreshTokenRotationStatus Status,
    RefreshSessionResult? Session);
