namespace HRMS.Infrastructure.Seeding;

public interface IDevelopmentSeedService
{
    Task<DevelopmentSeedResult> SeedAsync(CancellationToken cancellationToken = default);
}

public sealed record DevelopmentSeedResult(
    Guid TenantId,
    string TenantCode,
    Guid UserId,
    string UserEmail,
    Guid MembershipId,
    Guid CompanyId,
    string CompanyCode,
    Guid RoleId,
    int GrantedPermissionCount,
    bool UserCreated);
