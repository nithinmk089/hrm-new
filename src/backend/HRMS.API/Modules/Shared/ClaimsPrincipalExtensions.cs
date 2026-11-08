using System.Security.Claims;

namespace HRMS.API.Modules.Shared;

public static class ClaimsPrincipalExtensions
{
    public static Guid? ReadUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && userId != Guid.Empty ? userId : null;
    }

    public static Guid? ReadTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId) &&
        tenantId != Guid.Empty
            ? tenantId
            : null;
}
