namespace HRMS.Application.Abstractions.Security;

/// <summary>Credentials and tenant selection supplied to the login endpoint.</summary>
public sealed record LoginRequest(string Email, string Password, string TenantCode);
