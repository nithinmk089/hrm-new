using System.Security.Claims;
using HRMS.API.Authorization;
using HRMS.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task AuthorizeAsync_UsesValidatedClaimsAndRouteScope()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var permissionService = new RecordingPermissionAuthorizationService(true);
        var (authorizationService, accessor) = CreateAuthorizationService(permissionService);

        var context = new DefaultHttpContext();
        context.RequestAborted = CancellationToken.None;
        context.Request.RouteValues["companyId"] = companyId.ToString();
        context.User = CreatePrincipal(userId, tenantId, membershipId);
        accessor.HttpContext = context;

        var result = await authorizationService.AuthorizeAsync(
            context.User, null, "Permission:employees.read");

        Assert.True(result.Succeeded);
        Assert.NotNull(permissionService.Request);
        Assert.Equal(userId, permissionService.Request!.UserId);
        Assert.Equal(tenantId, permissionService.Request.TenantId);
        Assert.Equal(membershipId, permissionService.Request.MembershipId);
        Assert.Equal(companyId, permissionService.Request.CompanyId);
        Assert.Equal("employees.read", permissionService.Request.PermissionCode);
    }

    [Fact]
    public async Task AuthorizeAsync_MissingIdentityClaims_DeniesWithoutCallingService()
    {
        var permissionService = new RecordingPermissionAuthorizationService(true);
        var (authorizationService, accessor) = CreateAuthorizationService(permissionService);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        accessor.HttpContext = context;

        var result = await authorizationService.AuthorizeAsync(
            context.User, null, "Permission:employees.read");

        Assert.False(result.Succeeded);
        Assert.Null(permissionService.Request);
    }

    [Fact]
    public async Task AuthorizeAsync_InvalidRouteScope_DeniesWithoutCallingService()
    {
        var permissionService = new RecordingPermissionAuthorizationService(true);
        var (authorizationService, accessor) = CreateAuthorizationService(permissionService);
        var context = new DefaultHttpContext
        {
            User = CreatePrincipal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        };
        context.Request.RouteValues["officeId"] = Guid.NewGuid().ToString();
        accessor.HttpContext = context;

        var result = await authorizationService.AuthorizeAsync(
            context.User, null, "Permission:employees.read");

        Assert.False(result.Succeeded);
        Assert.Null(permissionService.Request);
    }

    private static (IAuthorizationService AuthorizationService, IHttpContextAccessor Accessor)
        CreateAuthorizationService(RecordingPermissionAuthorizationService permissionService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IPermissionAuthorizationService>(permissionService);

        var provider = services.BuildServiceProvider();
        return (
            provider.GetRequiredService<IAuthorizationService>(),
            provider.GetRequiredService<IHttpContextAccessor>());
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, Guid tenantId, Guid membershipId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("membership_id", membershipId.ToString())
        ], "Test");

        return new ClaimsPrincipal(identity);
    }

    private sealed class RecordingPermissionAuthorizationService(bool result)
        : IPermissionAuthorizationService
    {
        public PermissionAuthorizationRequest? Request { get; private set; }

        public Task<bool> HasPermissionAsync(
            PermissionAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
