using HRMS.API.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class PermissionPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_PermissionPolicy_CreatesAuthenticatedRequirement()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("Permission: employees.read ");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement =>
            requirement is PermissionRequirement permission &&
            permission.PermissionCode == "employees.read");
        Assert.Contains(policy.Requirements, requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task GetPolicyAsync_EmptyPermission_ReturnsNull()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("Permission: ");

        Assert.Null(policy);
    }
}
