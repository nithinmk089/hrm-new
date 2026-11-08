namespace HRMS.Application.Abstractions.Security;

public interface IAuthenticationService
{
    /// <summary>Returns null for invalid credentials or when a secure session cannot be created.</summary>
    Task<LoginSessionResult?> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
