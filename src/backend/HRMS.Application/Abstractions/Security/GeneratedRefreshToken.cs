namespace HRMS.Application.Abstractions.Security;

/// <summary>
/// A newly generated opaque refresh token and its SHA-256 digest.
/// The raw value is only for delivery to the client and must never be persisted or logged.
/// </summary>
public sealed record GeneratedRefreshToken(string Value, byte[] Hash);
