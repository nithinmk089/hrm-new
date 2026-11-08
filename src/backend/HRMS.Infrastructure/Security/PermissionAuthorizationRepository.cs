using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
namespace HRMS.Infrastructure.Security;
/// <summary>Resolves permissions from active identity, role grants and requested company/office scope.</summary>
internal sealed class PermissionAuthorizationRepository(IDbConnectionFactory connectionFactory) : IPermissionAuthorizationRepository
{
    public async Task<bool> HasPermissionAsync(PermissionAuthorizationRequest request, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        const string sql = """
            ;WITH CompanyAncestors AS
            (
                SELECT c.CompanyId, c.ParentCompanyId,
                    CAST('/' + CONVERT(varchar(36), c.CompanyId) + '/' AS varchar(max)) AS Path
                FROM hrms.Companies AS c
                WHERE c.TenantId = @TenantId AND c.CompanyId = @CompanyId AND c.DeletedAtUtc IS NULL
                UNION ALL
                SELECT parent.CompanyId, parent.ParentCompanyId,
                    CAST(child.Path + CONVERT(varchar(36), parent.CompanyId) + '/' AS varchar(max))
                FROM hrms.Companies AS parent
                INNER JOIN CompanyAncestors AS child ON child.ParentCompanyId = parent.CompanyId
                    AND parent.TenantId = @TenantId AND parent.DeletedAtUtc IS NULL
                WHERE CHARINDEX('/' + CONVERT(varchar(36), parent.CompanyId) + '/', child.Path) = 0
            )
            SELECT CAST(CASE WHEN EXISTS
            (
                SELECT 1
                FROM security.Users AS u
                INNER JOIN security.UserTenantMemberships AS membership
                    ON membership.UserId = u.UserId AND membership.TenantId = @TenantId
                    AND membership.MembershipId = @MembershipId AND membership.MembershipStatus = 'Active'
                INNER JOIN platform.Tenants AS tenant
                    ON tenant.TenantId = membership.TenantId AND tenant.Status = 'Active' AND tenant.IsDeleted = 0
                INNER JOIN security.UserRoleAssignments AS assignment
                    ON assignment.TenantId = membership.TenantId AND assignment.UserId = membership.UserId
                    AND (assignment.ExpiresAtUtc IS NULL OR assignment.ExpiresAtUtc > @NowUtc)
                INNER JOIN security.Roles AS role
                    ON role.TenantId = assignment.TenantId AND role.RoleId = assignment.RoleId AND role.IsActive = 1
                INNER JOIN security.RolePermissions AS rolePermission
                    ON rolePermission.TenantId = role.TenantId AND rolePermission.RoleId = role.RoleId
                INNER JOIN security.Permissions AS permission
                    ON permission.PermissionId = rolePermission.PermissionId
                    AND permission.PermissionCode = @PermissionCode AND permission.IsActive = 1
                WHERE u.UserId = @UserId AND u.IsEnabled = 1 AND u.IsDeleted = 0
                  AND (@CompanyId IS NULL OR EXISTS
                    (SELECT 1 FROM hrms.Companies requestedCompany
                     WHERE requestedCompany.TenantId = @TenantId AND requestedCompany.CompanyId = @CompanyId
                       AND requestedCompany.DeletedAtUtc IS NULL))
                  AND (@OfficeId IS NULL OR EXISTS
                    (SELECT 1 FROM hrms.Offices requestedOffice
                     WHERE requestedOffice.TenantId = @TenantId AND requestedOffice.CompanyId = @CompanyId
                       AND requestedOffice.OfficeId = @OfficeId AND requestedOffice.DeletedAtUtc IS NULL))
                  AND
                  (
                    (assignment.ScopeType = 'Tenant' AND assignment.CompanyId IS NULL AND assignment.OfficeId IS NULL)
                    OR
                    (assignment.ScopeType = 'Company' AND @CompanyId IS NOT NULL AND assignment.OfficeId IS NULL
                      AND (assignment.CompanyId = @CompanyId OR
                        (assignment.IncludeDescendants = 1 AND EXISTS
                          (SELECT 1 FROM CompanyAncestors ancestor
                           WHERE ancestor.CompanyId = assignment.CompanyId AND ancestor.CompanyId <> @CompanyId))))
                    OR
                    (assignment.ScopeType = 'Office' AND @CompanyId IS NOT NULL AND @OfficeId IS NOT NULL
                      AND assignment.CompanyId = @CompanyId AND assignment.OfficeId = @OfficeId
                      AND assignment.IncludeDescendants = 0)
                  )
            ) THEN 1 ELSE 0 END AS bit) AS HasPermission
            OPTION (MAXRECURSION 100);
            """;
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new {
            request.UserId, request.TenantId, request.MembershipId, request.PermissionCode,
            request.CompanyId, request.OfficeId, NowUtc = nowUtc
        }, cancellationToken: cancellationToken));
    }
}