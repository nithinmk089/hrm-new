namespace HRMS.Application.Abstractions.Security;

/// <summary>Application-level permission evaluation for an authenticated membership and resource scope.</summary>
public interface IPermissionAuthorizationService
{
    Task<bool> HasPermissionAsync(
        PermissionAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
