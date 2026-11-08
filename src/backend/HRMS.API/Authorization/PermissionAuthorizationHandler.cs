using System.Security.Claims;
using HRMS.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace HRMS.API.Authorization;

/// <summary>
/// Evaluates permissions using identity claims from the validated principal and
/// resource scope identifiers from route values. The repository verifies scope
/// against tenant-owned database records.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    IPermissionAuthorizationService permissionAuthorizationService,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue("sub");
        var tenantIdValue = context.User.FindFirstValue("tenant_id");
        var membershipIdValue = context.User.FindFirstValue("membership_id");

        if (!Guid.TryParse(userIdValue, out var userId) ||
            !Guid.TryParse(tenantIdValue, out var tenantId) ||
            !Guid.TryParse(membershipIdValue, out var membershipId))
        {
            context.Fail();
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            context.Fail();
            return;
        }

        if (!TryReadRouteGuid(httpContext, "companyId", out var companyId, out var hasCompanyId) ||
            !TryReadRouteGuid(httpContext, "officeId", out var officeId, out var hasOfficeId) ||
            (hasOfficeId && !hasCompanyId))
        {
            context.Fail();
            return;
        }

        var request = new PermissionAuthorizationRequest(
            userId,
            tenantId,
            membershipId,
            requirement.PermissionCode,
            hasCompanyId ? companyId : null,
            hasOfficeId ? officeId : null);

        var allowed = await permissionAuthorizationService.HasPermissionAsync(
            request, httpContext.RequestAborted);

        if (allowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }

    private static bool TryReadRouteGuid(
        HttpContext context,
        string routeKey,
        out Guid value,
        out bool isPresent)
    {
        value = Guid.Empty;
        isPresent = context.Request.RouteValues.TryGetValue(routeKey, out var rawValue);

        if (!isPresent)
        {
            return true;
        }

        return Guid.TryParse(Convert.ToString(rawValue), out value);
    }
}
