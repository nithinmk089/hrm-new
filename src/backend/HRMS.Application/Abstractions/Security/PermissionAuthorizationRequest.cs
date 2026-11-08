namespace HRMS.Application.Abstractions.Security;

/// <summary>
/// Permission check context. Tenant, user and membership IDs must come from
/// validated authentication claims, never directly from an untrusted request body.
/// </summary>
public sealed record PermissionAuthorizationRequest(
    Guid UserId,
    Guid TenantId,
    Guid MembershipId,
    string PermissionCode,
    Guid? CompanyId = null,
    Guid? OfficeId = null);
