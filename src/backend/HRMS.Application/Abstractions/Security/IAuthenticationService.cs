namespace HRMS.Application.Abstractions.Security;

public interface IAuthenticationService
{
    /// <summary>Returns null for invalid, disabled, locked, or non-member credentials.</summary>
    Task<LoginResult?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
