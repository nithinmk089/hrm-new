using HRMS.Application.Abstractions.Security;

namespace HRMS.Application.Security;

/// <summary>Validates authorization context and delegates effective-grant evaluation.</summary>
public sealed class PermissionAuthorizationService(
    IPermissionAuthorizationRepository repository,
    TimeProvider timeProvider) : IPermissionAuthorizationService
{
    public Task<bool> HasPermissionAsync(
        PermissionAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ArgumentOutOfRangeException.ThrowIfEqual(request.UserId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(request.TenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(request.MembershipId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PermissionCode);

        if (request.OfficeId.HasValue && !request.CompanyId.HasValue)
        {
            throw new ArgumentException(
                "CompanyId is required when OfficeId is supplied.",
                nameof(request));
        }

        return repository.HasPermissionAsync(
            request with { PermissionCode = request.PermissionCode.Trim() },
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }
}
