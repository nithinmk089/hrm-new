using System.Security.Claims;

namespace HRMS.API.Modules.Shared;

public static class ClaimsPrincipalExtensions
{
    public static Guid? ReadTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId) &&
        tenantId != Guid.Empty
            ? tenantId
            : null;
}
