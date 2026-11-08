using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Seeding;

internal sealed class DevelopmentSeedService(
    IDbConnectionFactory connectionFactory,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : IDevelopmentSeedService
{
    private const string Actor = "HRMS development bootstrap";

    public async Task<DevelopmentSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings(configuration);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var tenantId = await EnsureTenantAsync(connection, transaction, settings, cancellationToken);
            var existingUserId = await FindUserAsync(connection, transaction, settings.UserEmail, cancellationToken);
            var userCreated = existingUserId is null;
            var userId = existingUserId ?? await CreateUserAsync(connection, transaction, settings, cancellationToken);

            await EnsureActiveUserAsync(connection, transaction, userId, cancellationToken);
            var membershipId = await EnsureMembershipAsync(connection, transaction, tenantId, userId, cancellationToken);
            var companyId = await EnsureCompanyAsync(connection, transaction, tenantId, userId, settings, cancellationToken);
            var roleId = await EnsureAdministratorRoleAsync(connection, transaction, tenantId, settings, cancellationToken);
            var grantedPermissionCount = await GrantActivePermissionsAsync(connection, transaction, tenantId, roleId, cancellationToken);
            await EnsureTenantRoleAssignmentAsync(connection, transaction, tenantId, userId, roleId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new DevelopmentSeedResult(
                tenantId, settings.TenantCode, userId, settings.UserEmail,
                membershipId, companyId, settings.CompanyCode, roleId,
                grantedPermissionCount, userCreated);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static SeedSettings ReadSettings(IConfiguration configuration)
    {
        var settings = new SeedSettings(
            Required(configuration, "DevelopmentSeed:TenantCode"),
            Required(configuration, "DevelopmentSeed:TenantName"),
            Required(configuration, "DevelopmentSeed:Email"),
            configuration["DevelopmentSeed:Password"],
            Required(configuration, "DevelopmentSeed:DisplayName"),
            Required(configuration, "DevelopmentSeed:CompanyCode"),
            Required(configuration, "DevelopmentSeed:CompanyLegalName"),
            Required(configuration, "DevelopmentSeed:CompanyDisplayName"),
            configuration["DevelopmentSeed:CountryCode"] ?? "IN",
            configuration["DevelopmentSeed:CurrencyCode"] ?? "INR",
            configuration["DevelopmentSeed:TimeZoneId"] ?? "Asia/Kolkata",
            configuration["DevelopmentSeed:RoleCode"] ?? "SYSTEM_ADMIN",
            configuration["DevelopmentSeed:RoleName"] ?? "System Administrator");

        if (settings.TenantCode.Length > 50 ||
            settings.UserEmail.Length > 320 ||
            settings.DisplayName.Length > 200 ||
            settings.CompanyCode.Length > 30 ||
            settings.CompanyLegalName.Length > 200 ||
            settings.CompanyDisplayName.Length > 150 ||
            settings.RoleCode.Length > 80 ||
            settings.RoleName.Length > 150)
            throw new InvalidOperationException("One or more DevelopmentSeed values exceed their database column length.");

        if (settings.CountryCode.Length != 2 || settings.CurrencyCode.Length != 3)
            throw new InvalidOperationException("DevelopmentSeed country and currency codes have invalid lengths.");

        return settings;
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Development seed configuration '{key}' is required.");
        return value;
    }

    private async Task<Guid> EnsureTenantAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        SeedSettings settings,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET NOCOUNT ON;
            DECLARE @TenantId uniqueidentifier, @Status varchar(20), @IsDeleted bit;

            SELECT @TenantId = TenantId, @Status = Status, @IsDeleted = IsDeleted
            FROM platform.Tenants WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantCode = @TenantCode;

            IF @TenantId IS NULL
            BEGIN
                SET @TenantId = NEWID();
                INSERT INTO platform.Tenants
                    (TenantId, TenantCode, TenantName, Status, DefaultTimeZoneId,
                     DefaultCulture, CreatedBy, IsDeleted)
                VALUES
                    (@TenantId, @TenantCode, @TenantName, 'Active', @TimeZoneId,
                     'en-IN', @Actor, 0);
            END
            ELSE IF @IsDeleted = 1 OR @Status <> 'Active'
                THROW 51101, 'The configured development tenant exists but is not active.', 1;

            SELECT @TenantId;
            """;

        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            sql, new { settings.TenantCode, settings.TenantName, settings.TimeZoneId, Actor },
            transaction, cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> FindUserAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string email,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT UserId FROM security.Users WITH (UPDLOCK, HOLDLOCK)
            WHERE NormalizedEmail = UPPER(LTRIM(RTRIM(@Email)));
            """;

        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            sql, new { Email = email }, transaction, cancellationToken: cancellationToken));
    }

    private async Task<Guid> CreateUserAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        SeedSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Password) || settings.Password.Length < 12)
            throw new InvalidOperationException("A development password of at least 12 characters is required when creating the development user.");

        var passwordHash = passwordHasher.HashPassword(settings.Password);
        const string sql = """
            DECLARE @UserId uniqueidentifier = NEWID();
            INSERT INTO security.Users
                (UserId, Email, DisplayName, PasswordHash, IsEmailConfirmed,
                 IsEnabled, FailedAccessCount, CreatedBy, IsDeleted)
            VALUES
                (@UserId, @Email, @DisplayName, @PasswordHash, 1, 1, 0, @Actor, 0);
            SELECT @UserId;
            """;

        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            sql,
            new { Email = settings.UserEmail, settings.DisplayName, PasswordHash = passwordHash, Actor },
            transaction, cancellationToken: cancellationToken));
    }

    private static async Task EnsureActiveUserAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM security.Users
                WHERE UserId = @UserId AND IsEnabled = 1 AND IsDeleted = 0)
                THROW 51102, 'The configured development user exists but is disabled or deleted.', 1;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<Guid> EnsureMembershipAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET NOCOUNT ON;
            DECLARE @MembershipId uniqueidentifier, @MembershipStatus varchar(20);

            SELECT @MembershipId = MembershipId, @MembershipStatus = MembershipStatus
            FROM security.UserTenantMemberships WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND UserId = @UserId;

            IF @MembershipId IS NULL
            BEGIN
                SET @MembershipId = NEWID();
                INSERT INTO security.UserTenantMemberships
                    (MembershipId, TenantId, UserId, MembershipStatus,
                     IsDefault, JoinedAtUtc, CreatedByUserId)
                VALUES
                    (@MembershipId, @TenantId, @UserId, 'Active',
                     1, SYSUTCDATETIME(), @UserId);
            END
            ELSE IF @MembershipStatus <> 'Active'
                THROW 51103, 'The development user has a non-active membership in the configured tenant.', 1;

            SELECT @MembershipId;
            """;

        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            sql, new { TenantId = tenantId, UserId = userId }, transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid> EnsureCompanyAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid userId,
        SeedSettings settings,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET NOCOUNT ON;
            DECLARE @CompanyId uniqueidentifier, @ExistingTenantId uniqueidentifier;
            DECLARE @DeletedAtUtc datetime2(7);

            SELECT @CompanyId = CompanyId, @ExistingTenantId = TenantId, @DeletedAtUtc = DeletedAtUtc
            FROM hrms.Companies WITH (UPDLOCK, HOLDLOCK)
            WHERE CompanyCode = @CompanyCode;

            IF @CompanyId IS NULL
            BEGIN
                SET @CompanyId = NEWID();
                INSERT INTO hrms.Companies
                    (CompanyId, TenantId, CompanyCode, LegalName, DisplayName,
                     CountryCode, DefaultCurrencyCode, TimeZoneId, IsActive, CreatedByUserId)
                VALUES
                    (@CompanyId, @TenantId, @CompanyCode, @LegalName, @DisplayName,
                     @CountryCode, @CurrencyCode, @TimeZoneId, 1, @UserId);
            END
            ELSE IF @ExistingTenantId <> @TenantId OR @DeletedAtUtc IS NOT NULL
                THROW 51104, 'The configured company code is already used by another tenant or a deleted company.', 1;
            ELSE IF NOT EXISTS (SELECT 1 FROM hrms.Companies WHERE CompanyId = @CompanyId AND IsActive = 1)
                THROW 51105, 'The configured development company exists but is inactive.', 1;

            SELECT @CompanyId;
            """;

        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId, UserId = userId, settings.CompanyCode,
                LegalName = settings.CompanyLegalName, DisplayName = settings.CompanyDisplayName,
                CountryCode = settings.CountryCode, CurrencyCode = settings.CurrencyCode,
                settings.TimeZoneId
            },
            transaction, cancellationToken: cancellationToken));
    }

    private static async Task<Guid> EnsureAdministratorRoleAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        SeedSettings settings,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET NOCOUNT ON;
            DECLARE @RoleId uniqueidentifier;

            SELECT @RoleId = RoleId
            FROM security.Roles WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND RoleCode = @RoleCode;

            IF @RoleId IS NULL
            BEGIN
                SET @RoleId = NEWID();
                INSERT INTO security.Roles
                    (RoleId, TenantId, RoleCode, RoleName, Description,
                     IsSystemRole, IsActive, CreatedBy)
                VALUES
                    (@RoleId, @TenantId, @RoleCode, @RoleName,
                     'Local development administrator role.', 1, 1, @Actor);
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM security.Roles
                WHERE TenantId = @TenantId AND RoleId = @RoleId AND IsActive = 1)
                THROW 51106, 'The development administrator role exists but is inactive.', 1;

            SELECT @RoleId;
            """;

        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            sql, new { TenantId = tenantId, settings.RoleCode, settings.RoleName, Actor },
            transaction, cancellationToken: cancellationToken));
    }

    private static async Task<int> GrantActivePermissionsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF
            (
                SELECT COUNT(*)
                FROM security.Permissions
                WHERE IsActive = 1
                  AND PermissionCode IN
                  (
                      'companies.read', 'companies.create', 'companies.update', 'companies.deactivate',
                      'departments.read', 'departments.create', 'departments.update', 'departments.deactivate',
                      'offices.read', 'offices.create', 'offices.update', 'offices.deactivate',
                      'designations.read', 'designations.create', 'designations.update', 'designations.deactivate'
                  )
            ) <> 16
                THROW 51107, 'Company and organization permission catalogues are incomplete. Apply database migrations 007 and 008 before seeding development data.', 1;

            INSERT INTO security.RolePermissions (TenantId, RoleId, PermissionId, GrantedBy)
            SELECT @TenantId, @RoleId, p.PermissionId, @Actor
            FROM security.Permissions AS p
            WHERE p.IsActive = 1
              AND NOT EXISTS
              (
                  SELECT 1 FROM security.RolePermissions AS existing
                  WHERE existing.TenantId = @TenantId
                    AND existing.RoleId = @RoleId
                    AND existing.PermissionId = p.PermissionId
              );

            SELECT COUNT(*) FROM security.RolePermissions
            WHERE TenantId = @TenantId AND RoleId = @RoleId;
            """;

        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            sql, new { TenantId = tenantId, RoleId = roleId, Actor }, transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureTenantRoleAssignmentAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS
            (
                SELECT 1 FROM security.UserRoleAssignments WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = @TenantId AND UserId = @UserId AND RoleId = @RoleId
                  AND ScopeType = 'Tenant' AND CompanyId IS NULL AND OfficeId IS NULL
                  AND ExpiresAtUtc IS NULL
            )
            BEGIN
                INSERT INTO security.UserRoleAssignments
                    (TenantId, UserId, RoleId, ScopeType, CompanyId, OfficeId,
                     IncludeDescendants, AssignedByUserId)
                VALUES
                    (@TenantId, @UserId, @RoleId, 'Tenant', NULL, NULL, 0, @UserId);
            END;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { TenantId = tenantId, UserId = userId, RoleId = roleId },
            transaction, cancellationToken: cancellationToken));
    }

    private sealed record SeedSettings(
        string TenantCode,
        string TenantName,
        string UserEmail,
        string? Password,
        string DisplayName,
        string CompanyCode,
        string CompanyLegalName,
        string CompanyDisplayName,
        string CountryCode,
        string CurrencyCode,
        string TimeZoneId,
        string RoleCode,
        string RoleName);
}
