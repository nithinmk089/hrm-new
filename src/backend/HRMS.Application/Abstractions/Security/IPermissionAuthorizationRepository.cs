namespace HRMS.Application.Abstractions.Security;

/// <summary>Evaluates active permission grants against the canonical tenant security schema.</summary>
public interface IPermissionAuthorizationRepository
{
    Task<bool> HasPermissionAsync(
        PermissionAuthorizationRequest request,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
