/*
    HRMS V1 - Migration 0002: Foundation stored procedures and permission catalog
    Requires migration 0001.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE platform.usp_DatabaseVersions_GetLatest
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) DatabaseVersionId, VersionNumber, Description, AppliedAtUtc, AppliedBy
    FROM platform.DatabaseVersions
    ORDER BY DatabaseVersionId DESC;
END;
GO

CREATE OR ALTER PROCEDURE platform.usp_Configurations_Get
    @TenantId uniqueidentifier,
    @ConfigurationKey varchar(150)
AS
BEGIN
    SET NOCOUNT ON;
    IF @TenantId IS NULL THROW 51001, 'TenantId is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ConfigurationKey)), '') IS NULL
        THROW 51002, 'ConfigurationKey is required.', 1;

    SELECT ConfigurationId, TenantId, ConfigurationKey, ConfigurationValue,
           ValueFormat, Description, IsSecret, UpdatedAtUtc, RowVersion
    FROM platform.Configurations
    WHERE TenantId = @TenantId
      AND ConfigurationKey = @ConfigurationKey
      AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE audit.usp_AuditLogs_Insert
    @TenantId uniqueidentifier = NULL,
    @ActorUserId uniqueidentifier = NULL,
    @EventType varchar(80),
    @EntityName varchar(150),
    @EntityId nvarchar(150) = NULL,
    @ActionName varchar(50),
    @CorrelationId uniqueidentifier = NULL,
    @SourceIp varchar(45) = NULL,
    @ChangesJson nvarchar(max) = NULL,
    @MetadataJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@EventType)), '') IS NULL THROW 51003, 'EventType is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@EntityName)), '') IS NULL THROW 51004, 'EntityName is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActionName)), '') IS NULL THROW 51005, 'ActionName is required.', 1;
    IF @ChangesJson IS NOT NULL AND ISJSON(@ChangesJson) <> 1 THROW 51006, 'ChangesJson must contain valid JSON.', 1;
    IF @MetadataJson IS NOT NULL AND ISJSON(@MetadataJson) <> 1 THROW 51007, 'MetadataJson must contain valid JSON.', 1;

    INSERT INTO audit.AuditLogs
    (TenantId, ActorUserId, EventType, EntityName, EntityId, ActionName, CorrelationId, SourceIp, ChangesJson, MetadataJson)
    VALUES
    (@TenantId, @ActorUserId, @EventType, @EntityName, @EntityId, @ActionName, @CorrelationId, @SourceIp, @ChangesJson, @MetadataJson);

    SELECT CONVERT(bigint, SCOPE_IDENTITY()) AS AuditLogId;
END;
GO

CREATE OR ALTER PROCEDURE audit.usp_ErrorLogs_Insert
    @TenantId uniqueidentifier = NULL,
    @UserId uniqueidentifier = NULL,
    @CorrelationId uniqueidentifier = NULL,
    @ApplicationName nvarchar(100),
    @EnvironmentName nvarchar(50),
    @ErrorCode varchar(100) = NULL,
    @Message nvarchar(2000),
    @ExceptionDetails nvarchar(max) = NULL,
    @RequestPath nvarchar(500) = NULL,
    @HttpMethod varchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@ApplicationName)), '') IS NULL THROW 51008, 'ApplicationName is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@EnvironmentName)), '') IS NULL THROW 51009, 'EnvironmentName is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@Message)), '') IS NULL THROW 51010, 'Message is required.', 1;

    INSERT INTO audit.ErrorLogs
    (TenantId, UserId, CorrelationId, ApplicationName, EnvironmentName, ErrorCode, Message, ExceptionDetails, RequestPath, HttpMethod)
    VALUES
    (@TenantId, @UserId, @CorrelationId, @ApplicationName, @EnvironmentName, @ErrorCode, @Message, @ExceptionDetails, @RequestPath, @HttpMethod);

    SELECT CONVERT(bigint, SCOPE_IDENTITY()) AS ErrorLogId;
END;
GO

/* Seed global permission catalog; tenant roles are mapped to these permissions. */
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'company.read')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('company.read', N'View company configuration', 'company', N'View company master data.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'company.manage')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('company.manage', N'Manage company configuration', 'company', N'Create, update and deactivate company master data.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'employees.read')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('employees.read', N'View employees', 'employees', N'View employee records within authorized scope.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'employees.manage')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('employees.manage', N'Manage employees', 'employees', N'Create and update employee records within authorized scope.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'leave.read')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('leave.read', N'View leave', 'leave', N'View leave requests and balances within authorized scope.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'leave.apply')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('leave.apply', N'Apply for leave', 'leave', N'Submit and manage own leave requests.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'leave.approve')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('leave.approve', N'Approve leave', 'leave', N'Approve or reject leave requests within assigned scope.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'timesheets.read')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('timesheets.read', N'View timesheets', 'timesheets', N'View timesheets within authorized scope.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'timesheets.manage')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('timesheets.manage', N'Manage timesheets', 'timesheets', N'Create and edit own timesheet entries.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'timesheets.approve')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('timesheets.approve', N'Approve timesheets', 'timesheets', N'Approve or reject submitted timesheets.');
IF NOT EXISTS (SELECT 1 FROM security.Permissions WHERE PermissionCode = 'users.manage')
    INSERT INTO security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
    VALUES ('users.manage', N'Manage users and access', 'security', N'Manage user accounts, roles and permissions.');
GO
