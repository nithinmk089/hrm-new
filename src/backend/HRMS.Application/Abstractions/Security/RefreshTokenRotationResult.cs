namespace HRMS.Application.Abstractions.Security;

/// <summary>Result of an atomic refresh-token rotation attempt.</summary>
public sealed record RefreshTokenRotationResult(
    RefreshTokenRotationStatus Status,
    UserLoginRecord? User);
