/*
    Migration: 005_TenantIdentityAndAuthorizationFoundation
    Purpose:
      - Make hrms.Companies tenant-owned and support same-tenant company hierarchy.
      - Enforce tenant ownership on all existing company-owned HRMS records.
      - Establish one global identity in security.Users with tenant memberships.
      - Add explicit tenant/company/office-scoped role assignments.
      - Preserve the existing security.Permissions catalogue and permission IDs.
      - Keep audit.AuditLogs as the canonical audit store.

    Important:
      - Migrations 001-004 are immutable.
      - Existing business/security tables were inspected and reported empty,
        except security.Permissions (11 rows). Permission rows are not changed.
      - Legacy hrms identity/security/audit tables are retained in this migration
        to avoid destructive cleanup before application code has been switched.
        They are deprecated and must not be used by new application code.
      - The migration runner executes this script in one transaction.
*/

SET XACT_ABORT ON;

-------------------------------------------------------------------------------
-- 1. Validate prerequisites and protect the known permission catalogue.
-------------------------------------------------------------------------------
IF SCHEMA_ID(N'platform') IS NULL OR OBJECT_ID(N'platform.Tenants', N'U') IS NULL
    THROW 51001, 'Required table platform.Tenants does not exist.', 1;

IF SCHEMA_ID(N'security') IS NULL
    THROW 51002, 'Required schema security does not exist.', 1;

IF OBJECT_ID(N'security.Users', N'U') IS NULL
   OR OBJECT_ID(N'security.Roles', N'U') IS NULL
   OR OBJECT_ID(N'security.Permissions', N'U') IS NULL
   OR OBJECT_ID(N'security.UserRoles', N'U') IS NULL
   OR OBJECT_ID(N'security.RolePermissions', N'U') IS NULL
   OR OBJECT_ID(N'security.RefreshTokens', N'U') IS NULL
    THROW 51003, 'One or more required security tables do not exist.', 1;

IF (SELECT COUNT_BIG(*) FROM security.Permissions) <> 11
    THROW 51004, 'security.Permissions row count differs from the inspected baseline of 11. Migration stopped to protect permission data.', 1;

IF OBJECT_ID(N'audit.AuditLogs', N'U') IS NULL
    THROW 51005, 'Required canonical table audit.AuditLogs does not exist.', 1;

IF EXISTS (SELECT 1 FROM hrms.Companies)
    THROW 51006, 'hrms.Companies is not empty. A reviewed tenant backfill is required before this migration can run.', 1;

-------------------------------------------------------------------------------
-- 2. Globalize security.Users.
--    Existing tenant/user relationships are moved to UserTenantMemberships.
--    Current table is empty, so no identity rows need backfilling.
-------------------------------------------------------------------------------
IF COL_LENGTH(N'security.Users', N'TenantId') IS NULL
    THROW 51007, 'security.Users does not have the expected TenantId column.', 1;

IF EXISTS (SELECT 1 FROM security.Users)
    THROW 51008, 'security.Users contains rows. Global identity conversion requires a reviewed backfill and is intentionally blocked.', 1;

DECLARE @DropUserForeignKeys nvarchar(max) = N'';

SELECT @DropUserForeignKeys = @DropUserForeignKeys
    + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(parent_object.schema_id))
    + N'.' + QUOTENAME(parent_object.name)
    + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
FROM sys.foreign_keys AS fk
INNER JOIN sys.tables AS parent_object ON parent_object.object_id = fk.parent_object_id
WHERE fk.referenced_object_id = OBJECT_ID(N'security.Users')
   OR fk.parent_object_id = OBJECT_ID(N'security.Users');

IF @DropUserForeignKeys <> N''
    EXEC sys.sp_executesql @DropUserForeignKeys;

DECLARE @DropUserTenantUniqueConstraints nvarchar(max) = N'';

SELECT @DropUserTenantUniqueConstraints = @DropUserTenantUniqueConstraints
    + N'ALTER TABLE [security].[Users] DROP CONSTRAINT '
    + QUOTENAME(kc.name) + N';' + CHAR(10)
FROM sys.key_constraints AS kc
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID(N'security.Users')
  AND kc.type = N'UQ'
  AND c.name = N'TenantId';

IF @DropUserTenantUniqueConstraints <> N''
    EXEC sys.sp_executesql @DropUserTenantUniqueConstraints;

DECLARE @DropUserTenantIndexes nvarchar(max) = N'';

SELECT @DropUserTenantIndexes = @DropUserTenantIndexes
    + N'DROP INDEX ' + QUOTENAME(i.name) + N' ON [security].[Users];' + CHAR(10)
FROM sys.indexes AS i
WHERE i.object_id = OBJECT_ID(N'security.Users')
  AND i.index_id > 0
  AND i.is_primary_key = 0
  AND i.is_unique_constraint = 0
  AND EXISTS
  (
      SELECT 1
      FROM sys.index_columns AS ic
      INNER JOIN sys.columns AS c
          ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE ic.object_id = i.object_id
        AND ic.index_id = i.index_id
        AND c.name = N'TenantId'
  );

IF @DropUserTenantIndexes <> N''
    EXEC sys.sp_executesql @DropUserTenantIndexes;

ALTER TABLE [security].[Users] DROP COLUMN [TenantId];

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'security.Users')
      AND name = N'UX_Users_NormalizedEmail_Global'
)
    CREATE UNIQUE INDEX [UX_Users_NormalizedEmail_Global]
        ON [security].[Users] ([NormalizedEmail]);

-------------------------------------------------------------------------------
-- 3. Tenant membership and scoped role assignments.
-------------------------------------------------------------------------------
CREATE TABLE [security].[UserTenantMemberships]
(
    [MembershipId] uniqueidentifier NOT NULL
        CONSTRAINT [DF_UserTenantMemberships_MembershipId] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [MembershipStatus] varchar(20) NOT NULL
        CONSTRAINT [DF_UserTenantMemberships_Status] DEFAULT ('Invited'),
    [IsDefault] bit NOT NULL
        CONSTRAINT [DF_UserTenantMemberships_IsDefault] DEFAULT (0),
    [JoinedAtUtc] datetime2(7) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL
        CONSTRAINT [DF_UserTenantMemberships_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_UserTenantMemberships] PRIMARY KEY CLUSTERED ([MembershipId]),
    CONSTRAINT [UQ_UserTenantMemberships_Tenant_Membership]
        UNIQUE ([TenantId], [MembershipId]),
    CONSTRAINT [UQ_UserTenantMemberships_Tenant_User]
        UNIQUE ([TenantId], [UserId]),
    CONSTRAINT [FK_UserTenantMemberships_Tenant]
        FOREIGN KEY ([TenantId]) REFERENCES [platform].[Tenants] ([TenantId]),
    CONSTRAINT [FK_UserTenantMemberships_User]
        FOREIGN KEY ([UserId]) REFERENCES [security].[Users] ([UserId]),
    CONSTRAINT [CK_UserTenantMemberships_Status]
        CHECK ([MembershipStatus] IN ('Invited', 'Active', 'Suspended', 'Removed'))
);

CREATE UNIQUE INDEX [UX_UserTenantMemberships_OneDefaultPerUser]
    ON [security].[UserTenantMemberships] ([UserId])
    WHERE [IsDefault] = 1 AND [MembershipStatus] = 'Active';

CREATE INDEX [IX_UserTenantMemberships_User_Status]
    ON [security].[UserTenantMemberships] ([UserId], [MembershipStatus])
    INCLUDE ([TenantId], [IsDefault]);

-- Re-establish tenant membership references only after the parent table exists.
ALTER TABLE [security].[UserRoles]
    ADD CONSTRAINT [FK_UserRoles_TenantMembership]
    FOREIGN KEY ([TenantId], [UserId])
    REFERENCES [security].[UserTenantMemberships] ([TenantId], [UserId]);

ALTER TABLE [security].[RefreshTokens]
    ADD CONSTRAINT [FK_RefreshTokens_TenantMembership]
    FOREIGN KEY ([TenantId], [UserId])
    REFERENCES [security].[UserTenantMemberships] ([TenantId], [UserId]);

GO

-------------------------------------------------------------------------------
-- 4. Make companies tenant-owned and add same-tenant parent hierarchy.
-------------------------------------------------------------------------------
ALTER TABLE [hrms].[Companies]
    ADD [TenantId] uniqueidentifier NOT NULL,
        [ParentCompanyId] uniqueidentifier NULL;

GO

ALTER TABLE [hrms].[Companies]
    ADD CONSTRAINT [FK_Companies_Tenant]
        FOREIGN KEY ([TenantId]) REFERENCES [platform].[Tenants] ([TenantId]);

ALTER TABLE [hrms].[Companies]
    ADD CONSTRAINT [UQ_Companies_Tenant_Company]
        UNIQUE ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[Companies]
    ADD CONSTRAINT [FK_Companies_ParentCompany]
        FOREIGN KEY ([TenantId], [ParentCompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

CREATE INDEX [IX_Companies_Tenant_Active]
    ON [hrms].[Companies] ([TenantId], [IsActive])
    INCLUDE ([CompanyCode], [LegalName], [DisplayName])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_Companies_Tenant_Parent]
    ON [hrms].[Companies] ([TenantId], [ParentCompanyId])
    WHERE [ParentCompanyId] IS NOT NULL AND [DeletedAtUtc] IS NULL;

-------------------------------------------------------------------------------
-- 5. Add tenant ownership to every existing company-owned HRMS table.
--    Composite foreign keys prevent a row from claiming another tenant.
-------------------------------------------------------------------------------
ALTER TABLE [hrms].[Departments] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[Offices] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[Designations] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[Employees] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[EmployeeEmployment] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[EmployeeReportingAssignments] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[EmployeeContacts] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[EmployeeAddresses] ADD [TenantId] uniqueidentifier NOT NULL;
ALTER TABLE [hrms].[EmployeeDocuments] ADD [TenantId] uniqueidentifier NOT NULL;

ALTER TABLE [hrms].[Departments]
    ADD CONSTRAINT [FK_Departments_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[Offices]
    ADD CONSTRAINT [UQ_Offices_Tenant_Company_Office]
        UNIQUE ([TenantId], [CompanyId], [OfficeId]);

ALTER TABLE [hrms].[Offices]
    ADD CONSTRAINT [FK_Offices_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[Designations]
    ADD CONSTRAINT [FK_Designations_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[Employees]
    ADD CONSTRAINT [FK_Employees_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[EmployeeEmployment]
    ADD CONSTRAINT [FK_EmployeeEmployment_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[EmployeeReportingAssignments]
    ADD CONSTRAINT [FK_EmployeeReportingAssignments_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[EmployeeContacts]
    ADD CONSTRAINT [FK_EmployeeContacts_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[EmployeeAddresses]
    ADD CONSTRAINT [FK_EmployeeAddresses_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

ALTER TABLE [hrms].[EmployeeDocuments]
    ADD CONSTRAINT [FK_EmployeeDocuments_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]);

CREATE INDEX [IX_Departments_Tenant_Company]
    ON [hrms].[Departments] ([TenantId], [CompanyId], [IsActive])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_Offices_Tenant_Company]
    ON [hrms].[Offices] ([TenantId], [CompanyId], [IsActive])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_Designations_Tenant_Company]
    ON [hrms].[Designations] ([TenantId], [CompanyId], [IsActive])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_Employees_Tenant_Company]
    ON [hrms].[Employees] ([TenantId], [CompanyId], [IsActive])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_EmployeeEmployment_Tenant_Company_Employee]
    ON [hrms].[EmployeeEmployment] ([TenantId], [CompanyId], [EmployeeId])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_EmployeeReportingAssignments_Tenant_Company_Employee]
    ON [hrms].[EmployeeReportingAssignments] ([TenantId], [CompanyId], [EmployeeId])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_EmployeeContacts_Tenant_Company_Employee]
    ON [hrms].[EmployeeContacts] ([TenantId], [CompanyId], [EmployeeId])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_EmployeeAddresses_Tenant_Company_Employee]
    ON [hrms].[EmployeeAddresses] ([TenantId], [CompanyId], [EmployeeId])
    WHERE [DeletedAtUtc] IS NULL;

CREATE INDEX [IX_EmployeeDocuments_Tenant_Company_Employee]
    ON [hrms].[EmployeeDocuments] ([TenantId], [CompanyId], [EmployeeId])
    WHERE [DeletedAtUtc] IS NULL;

-------------------------------------------------------------------------------
-- 6. Create scoped role assignments.
--    ScopeType: Tenant, Company, Office.
--    Tenant scope has no company/office.
--    Company scope has a company and no office.
--    Office scope has both company and office.
-------------------------------------------------------------------------------
CREATE TABLE [security].[UserRoleAssignments]
(
    [UserRoleAssignmentId] uniqueidentifier NOT NULL
        CONSTRAINT [DF_UserRoleAssignments_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [ScopeType] varchar(20) NOT NULL,
    [CompanyId] uniqueidentifier NULL,
    [OfficeId] uniqueidentifier NULL,
    [IncludeDescendants] bit NOT NULL
        CONSTRAINT [DF_UserRoleAssignments_IncludeDescendants] DEFAULT (0),
    [AssignedAtUtc] datetime2(7) NOT NULL
        CONSTRAINT [DF_UserRoleAssignments_AssignedAtUtc] DEFAULT SYSUTCDATETIME(),
    [AssignedByUserId] uniqueidentifier NULL,
    [ExpiresAtUtc] datetime2(7) NULL,
    CONSTRAINT [PK_UserRoleAssignments]
        PRIMARY KEY CLUSTERED ([UserRoleAssignmentId]),
    CONSTRAINT [FK_UserRoleAssignments_Tenant]
        FOREIGN KEY ([TenantId]) REFERENCES [platform].[Tenants] ([TenantId]),
    CONSTRAINT [FK_UserRoleAssignments_Membership]
        FOREIGN KEY ([TenantId], [UserId])
        REFERENCES [security].[UserTenantMemberships] ([TenantId], [UserId]),
    CONSTRAINT [FK_UserRoleAssignments_Role]
        FOREIGN KEY ([TenantId], [RoleId])
        REFERENCES [security].[Roles] ([TenantId], [RoleId]),
    CONSTRAINT [FK_UserRoleAssignments_Company]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [FK_UserRoleAssignments_Office]
        FOREIGN KEY ([TenantId], [CompanyId], [OfficeId])
        REFERENCES [hrms].[Offices] ([TenantId], [CompanyId], [OfficeId]),
    CONSTRAINT [CK_UserRoleAssignments_Scope]
        CHECK
        (
            ([ScopeType] = 'Tenant' AND [CompanyId] IS NULL AND [OfficeId] IS NULL
                AND [IncludeDescendants] = 0)
            OR
            ([ScopeType] = 'Company' AND [CompanyId] IS NOT NULL AND [OfficeId] IS NULL)
            OR
            ([ScopeType] = 'Office' AND [CompanyId] IS NOT NULL AND [OfficeId] IS NOT NULL
                AND [IncludeDescendants] = 0)
        ),
    CONSTRAINT [CK_UserRoleAssignments_Expiry]
        CHECK ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > [AssignedAtUtc])
);

CREATE INDEX [IX_UserRoleAssignments_User_Tenant]
    ON [security].[UserRoleAssignments] ([TenantId], [UserId])
    INCLUDE ([RoleId], [ScopeType], [CompanyId], [OfficeId], [IncludeDescendants], [ExpiresAtUtc]);

CREATE INDEX [IX_UserRoleAssignments_Role_Scope]
    ON [security].[UserRoleAssignments] ([TenantId], [RoleId], [ScopeType], [CompanyId], [OfficeId]);

-------------------------------------------------------------------------------
-- 7. Make existing tenant-scoped role/token references use global user IDs.
--    Role definitions and role-permission mappings remain tenant scoped.
-------------------------------------------------------------------------------
-- UserRoles and RefreshTokens are tenant-scoped records. Their user foreign
-- keys are now global; membership validity is enforced by the new membership
-- table and assignment model.
IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'security.UserRoles')
      AND name = N'FK_UserRoles_TenantMembership'
)
    ALTER TABLE [security].[UserRoles]
        ADD CONSTRAINT [FK_UserRoles_GlobalUser]
        FOREIGN KEY ([UserId]) REFERENCES [security].[Users] ([UserId]);

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'security.RefreshTokens')
      AND name = N'FK_RefreshTokens_TenantMembership'
)
    ALTER TABLE [security].[RefreshTokens]
        ADD CONSTRAINT [FK_RefreshTokens_GlobalUser]
        FOREIGN KEY ([UserId]) REFERENCES [security].[Users] ([UserId]);

-------------------------------------------------------------------------------
-- 8. Enforce tenant and actor references on the canonical audit store.
-------------------------------------------------------------------------------
IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'audit.AuditLogs')
      AND name = N'FK_AuditLogs_Tenant'
)
    ALTER TABLE [audit].[AuditLogs]
        ADD CONSTRAINT [FK_AuditLogs_Tenant]
        FOREIGN KEY ([TenantId]) REFERENCES [platform].[Tenants] ([TenantId]);

IF COL_LENGTH(N'audit.AuditLogs', N'ActorUserId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.foreign_keys
       WHERE parent_object_id = OBJECT_ID(N'audit.AuditLogs')
         AND name = N'FK_AuditLogs_GlobalActor'
   )
    ALTER TABLE [audit].[AuditLogs]
        ADD CONSTRAINT [FK_AuditLogs_GlobalActor]
        FOREIGN KEY ([ActorUserId]) REFERENCES [security].[Users] ([UserId]);

-------------------------------------------------------------------------------
-- 9. Explicitly mark legacy duplicate HRMS security tables as deprecated.
--    No rows are deleted and no existing objects are dropped in this migration.
-------------------------------------------------------------------------------
EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.Users and security.UserTenantMemberships.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'Users';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.UserTenantMemberships.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'CompanyUserMemberships';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.Roles.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'Roles';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.Permissions.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'Permissions';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.UserRoles and security.UserRoleAssignments.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'UserRoles';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.RolePermissions.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'RolePermissions';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use security.RefreshTokens.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'RefreshTokens';

EXEC sys.sp_addextendedproperty
    @name = N'HRMS_Deprecated',
    @value = N'Legacy duplicate. Use audit.AuditLogs.',
    @level0type = N'SCHEMA', @level0name = N'hrms',
    @level1type = N'TABLE', @level1name = N'AuditLogs';
GO
