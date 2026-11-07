/*
    HRMS V1 - Migration 0001: Platform and security foundation
    Target: SQL Server 2022+, compatibility level 160
    Execute against the HRMS application database using a migration identity.

    IMPORTANT:
    - This script creates the initial foundation only; business-module tables come later.
    - TenantID values must be resolved by trusted backend authentication/tenant context.
    - Never accept a browser-supplied TenantID as proof of tenant membership.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_NAME() = N'master'
    THROW 51000, 'Run this migration against the HRMS application database, not master.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'platform') EXEC(N'CREATE SCHEMA platform');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'security') EXEC(N'CREATE SCHEMA security');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'audit') EXEC(N'CREATE SCHEMA audit');
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'platform.DatabaseVersions', N'U') IS NULL
    BEGIN
        CREATE TABLE platform.DatabaseVersions
        (
            DatabaseVersionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DatabaseVersions PRIMARY KEY,
            VersionNumber varchar(50) NOT NULL,
            Description nvarchar(500) NOT NULL,
            AppliedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DatabaseVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
            AppliedBy nvarchar(256) NOT NULL,
            ScriptChecksum char(64) NULL,
            CONSTRAINT UQ_DatabaseVersions_VersionNumber UNIQUE (VersionNumber)
        );
    END;

    IF OBJECT_ID(N'platform.Tenants', N'U') IS NULL
    BEGIN
        CREATE TABLE platform.Tenants
        (
            TenantId uniqueidentifier NOT NULL CONSTRAINT DF_Tenants_TenantId DEFAULT NEWSEQUENTIALID(),
            TenantCode varchar(50) NOT NULL,
            TenantName nvarchar(200) NOT NULL,
            NormalizedTenantName AS UPPER(LTRIM(RTRIM(TenantName))) PERSISTED,
            Status varchar(20) NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT ('Pending'),
            DefaultTimeZoneId nvarchar(100) NOT NULL CONSTRAINT DF_Tenants_TimeZone DEFAULT ('UTC'),
            DefaultCulture varchar(20) NOT NULL CONSTRAINT DF_Tenants_Culture DEFAULT ('en-IN'),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Tenants_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CreatedBy nvarchar(256) NOT NULL,
            UpdatedAtUtc datetime2(3) NULL,
            UpdatedBy nvarchar(256) NULL,
            IsDeleted bit NOT NULL CONSTRAINT DF_Tenants_IsDeleted DEFAULT (0),
            DeletedAtUtc datetime2(3) NULL,
            DeletedBy nvarchar(256) NULL,
            RowVersion rowversion NOT NULL,
            CONSTRAINT PK_Tenants PRIMARY KEY (TenantId),
            CONSTRAINT UQ_Tenants_TenantCode UNIQUE (TenantCode),
            CONSTRAINT CK_Tenants_Status CHECK (Status IN ('Pending','Active','Suspended','Closed'))
        );
        CREATE INDEX IX_Tenants_Status_IsDeleted ON platform.Tenants(Status, IsDeleted);
    END;

    IF OBJECT_ID(N'security.Permissions', N'U') IS NULL
    BEGIN
        CREATE TABLE security.Permissions
        (
            PermissionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
            PermissionCode varchar(150) NOT NULL,
            PermissionName nvarchar(200) NOT NULL,
            ModuleCode varchar(50) NOT NULL,
            Description nvarchar(500) NULL,
            IsSystemPermission bit NOT NULL CONSTRAINT DF_Permissions_IsSystem DEFAULT (1),
            IsActive bit NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT UQ_Permissions_PermissionCode UNIQUE (PermissionCode)
        );
        CREATE INDEX IX_Permissions_ModuleCode_IsActive ON security.Permissions(ModuleCode, IsActive);
    END;

    IF OBJECT_ID(N'security.Users', N'U') IS NULL
    BEGIN
        CREATE TABLE security.Users
        (
            UserId uniqueidentifier NOT NULL CONSTRAINT DF_Users_UserId DEFAULT NEWSEQUENTIALID(),
            TenantId uniqueidentifier NOT NULL,
            Email nvarchar(320) NOT NULL,
            NormalizedEmail AS UPPER(LTRIM(RTRIM(Email))) PERSISTED,
            DisplayName nvarchar(200) NOT NULL,
            PasswordHash nvarchar(1000) NOT NULL,
            IsEmailConfirmed bit NOT NULL CONSTRAINT DF_Users_IsEmailConfirmed DEFAULT (0),
            IsEnabled bit NOT NULL CONSTRAINT DF_Users_IsEnabled DEFAULT (1),
            FailedAccessCount int NOT NULL CONSTRAINT DF_Users_FailedAccessCount DEFAULT (0),
            LockoutEndUtc datetime2(3) NULL,
            LastLoginAtUtc datetime2(3) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CreatedBy nvarchar(256) NOT NULL,
            UpdatedAtUtc datetime2(3) NULL,
            UpdatedBy nvarchar(256) NULL,
            IsDeleted bit NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
            DeletedAtUtc datetime2(3) NULL,
            DeletedBy nvarchar(256) NULL,
            RowVersion rowversion NOT NULL,
            CONSTRAINT PK_Users PRIMARY KEY (UserId),
            CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES platform.Tenants(TenantId),
            CONSTRAINT UQ_Users_Tenant_User UNIQUE (TenantId, UserId),
            CONSTRAINT UQ_Users_Tenant_NormalizedEmail UNIQUE (TenantId, NormalizedEmail),
            CONSTRAINT CK_Users_FailedAccessCount CHECK (FailedAccessCount >= 0)
        );
        CREATE INDEX IX_Users_Tenant_IsEnabled_IsDeleted ON security.Users(TenantId, IsEnabled, IsDeleted);
    END;

    IF OBJECT_ID(N'security.Roles', N'U') IS NULL
    BEGIN
        CREATE TABLE security.Roles
        (
            RoleId uniqueidentifier NOT NULL CONSTRAINT DF_Roles_RoleId DEFAULT NEWSEQUENTIALID(),
            TenantId uniqueidentifier NOT NULL,
            RoleCode varchar(80) NOT NULL,
            RoleName nvarchar(150) NOT NULL,
            Description nvarchar(500) NULL,
            IsSystemRole bit NOT NULL CONSTRAINT DF_Roles_IsSystemRole DEFAULT (0),
            IsActive bit NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CreatedBy nvarchar(256) NOT NULL,
            UpdatedAtUtc datetime2(3) NULL,
            UpdatedBy nvarchar(256) NULL,
            RowVersion rowversion NOT NULL,
            CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
            CONSTRAINT FK_Roles_Tenants FOREIGN KEY (TenantId) REFERENCES platform.Tenants(TenantId),
            CONSTRAINT UQ_Roles_Tenant_Role UNIQUE (TenantId, RoleId),
            CONSTRAINT UQ_Roles_Tenant_RoleCode UNIQUE (TenantId, RoleCode)
        );
    END;

    IF OBJECT_ID(N'security.UserRoles', N'U') IS NULL
    BEGIN
        CREATE TABLE security.UserRoles
        (
            TenantId uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL,
            RoleId uniqueidentifier NOT NULL,
            AssignedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_UserRoles_AssignedAtUtc DEFAULT SYSUTCDATETIME(),
            AssignedBy nvarchar(256) NOT NULL,
            CONSTRAINT PK_UserRoles PRIMARY KEY (TenantId, UserId, RoleId),
            CONSTRAINT FK_UserRoles_Users FOREIGN KEY (TenantId, UserId) REFERENCES security.Users(TenantId, UserId),
            CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (TenantId, RoleId) REFERENCES security.Roles(TenantId, RoleId)
        );
        CREATE INDEX IX_UserRoles_Tenant_Role ON security.UserRoles(TenantId, RoleId, UserId);
    END;

    IF OBJECT_ID(N'security.RolePermissions', N'U') IS NULL
    BEGIN
        CREATE TABLE security.RolePermissions
        (
            TenantId uniqueidentifier NOT NULL,
            RoleId uniqueidentifier NOT NULL,
            PermissionId int NOT NULL,
            GrantedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_RolePermissions_GrantedAtUtc DEFAULT SYSUTCDATETIME(),
            GrantedBy nvarchar(256) NOT NULL,
            CONSTRAINT PK_RolePermissions PRIMARY KEY (TenantId, RoleId, PermissionId),
            CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (TenantId, RoleId) REFERENCES security.Roles(TenantId, RoleId),
            CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES security.Permissions(PermissionId)
        );
        CREATE INDEX IX_RolePermissions_Permission ON security.RolePermissions(PermissionId, TenantId, RoleId);
    END;

    IF OBJECT_ID(N'security.RefreshTokens', N'U') IS NULL
    BEGIN
        CREATE TABLE security.RefreshTokens
        (
            RefreshTokenId uniqueidentifier NOT NULL CONSTRAINT DF_RefreshTokens_Id DEFAULT NEWSEQUENTIALID(),
            TenantId uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL,
            TokenHash binary(32) NOT NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            ExpiresAtUtc datetime2(3) NOT NULL,
            RevokedAtUtc datetime2(3) NULL,
            ReplacedByTokenId uniqueidentifier NULL,
            CreatedByIp varchar(45) NULL,
            RevokedByIp varchar(45) NULL,
            RevocationReason nvarchar(200) NULL,
            CONSTRAINT PK_RefreshTokens PRIMARY KEY (RefreshTokenId),
            CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (TenantId, UserId) REFERENCES security.Users(TenantId, UserId),
            CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
            CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
        );
        CREATE INDEX IX_RefreshTokens_Tenant_User_Expiry ON security.RefreshTokens(TenantId, UserId, ExpiresAtUtc);
    END;

    IF OBJECT_ID(N'platform.Configurations', N'U') IS NULL
    BEGIN
        CREATE TABLE platform.Configurations
        (
            ConfigurationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Configurations PRIMARY KEY,
            TenantId uniqueidentifier NOT NULL,
            ConfigurationKey varchar(150) NOT NULL,
            ConfigurationValue nvarchar(max) NOT NULL,
            ValueFormat varchar(20) NOT NULL CONSTRAINT DF_Configurations_ValueFormat DEFAULT ('Text'),
            Description nvarchar(500) NULL,
            IsSecret bit NOT NULL CONSTRAINT DF_Configurations_IsSecret DEFAULT (0),
            IsActive bit NOT NULL CONSTRAINT DF_Configurations_IsActive DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Configurations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CreatedBy nvarchar(256) NOT NULL,
            UpdatedAtUtc datetime2(3) NULL,
            UpdatedBy nvarchar(256) NULL,
            RowVersion rowversion NOT NULL,
            CONSTRAINT FK_Configurations_Tenants FOREIGN KEY (TenantId) REFERENCES platform.Tenants(TenantId),
            CONSTRAINT UQ_Configurations_Tenant_Key UNIQUE (TenantId, ConfigurationKey),
            CONSTRAINT CK_Configurations_ValueFormat CHECK (ValueFormat IN ('Text','Number','Boolean','Json'))
        );
    END;

    IF OBJECT_ID(N'audit.AuditLogs', N'U') IS NULL
    BEGIN
        CREATE TABLE audit.AuditLogs
        (
            AuditLogId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
            TenantId uniqueidentifier NULL,
            ActorUserId uniqueidentifier NULL,
            EventType varchar(80) NOT NULL,
            EntityName varchar(150) NOT NULL,
            EntityId nvarchar(150) NULL,
            ActionName varchar(50) NOT NULL,
            OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AuditLogs_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
            CorrelationId uniqueidentifier NULL,
            SourceIp varchar(45) NULL,
            ChangesJson nvarchar(max) NULL,
            MetadataJson nvarchar(max) NULL,
            CONSTRAINT CK_AuditLogs_ChangesJson CHECK (ChangesJson IS NULL OR ISJSON(ChangesJson) = 1),
            CONSTRAINT CK_AuditLogs_MetadataJson CHECK (MetadataJson IS NULL OR ISJSON(MetadataJson) = 1)
        );
        CREATE INDEX IX_AuditLogs_Tenant_OccurredAt ON audit.AuditLogs(TenantId, OccurredAtUtc DESC);
        CREATE INDEX IX_AuditLogs_Entity ON audit.AuditLogs(TenantId, EntityName, EntityId, OccurredAtUtc DESC);
        CREATE INDEX IX_AuditLogs_CorrelationId ON audit.AuditLogs(CorrelationId) WHERE CorrelationId IS NOT NULL;
    END;

    IF OBJECT_ID(N'audit.ErrorLogs', N'U') IS NULL
    BEGIN
        CREATE TABLE audit.ErrorLogs
        (
            ErrorLogId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ErrorLogs PRIMARY KEY,
            TenantId uniqueidentifier NULL,
            UserId uniqueidentifier NULL,
            OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ErrorLogs_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
            CorrelationId uniqueidentifier NULL,
            ApplicationName nvarchar(100) NOT NULL,
            EnvironmentName nvarchar(50) NOT NULL,
            ErrorCode varchar(100) NULL,
            Message nvarchar(2000) NOT NULL,
            ExceptionDetails nvarchar(max) NULL,
            RequestPath nvarchar(500) NULL,
            HttpMethod varchar(10) NULL,
            CONSTRAINT CK_ErrorLogs_HttpMethod CHECK (HttpMethod IS NULL OR HttpMethod IN ('GET','POST','PUT','PATCH','DELETE','OPTIONS','HEAD'))
        );
        CREATE INDEX IX_ErrorLogs_OccurredAt ON audit.ErrorLogs(OccurredAtUtc DESC);
        CREATE INDEX IX_ErrorLogs_Tenant_OccurredAt ON audit.ErrorLogs(TenantId, OccurredAtUtc DESC);
    END;

    IF NOT EXISTS (SELECT 1 FROM platform.DatabaseVersions WHERE VersionNumber = '0001')
    BEGIN
        INSERT INTO platform.DatabaseVersions (VersionNumber, Description, AppliedBy)
        VALUES ('0001', N'Initial platform, tenant, security and audit foundation', ORIGINAL_LOGIN());
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/*
    Operational notes:
    1. PasswordHash must contain a vetted password-hasher output (never plaintext).
    2. Store only SHA-256 hashes of cryptographically random refresh tokens in TokenHash.
    3. AuditLogs are append-only from the application perspective; deny UPDATE/DELETE to
       the normal application database principal.
    4. Keep ErrorLogs access restricted because exception details can contain sensitive data.
    5. Tenant-scoped procedures must receive TenantId from trusted server context and must
       include it in every read/update/delete predicate.
*/
