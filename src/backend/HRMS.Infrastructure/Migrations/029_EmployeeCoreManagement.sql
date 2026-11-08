/*
    Migration: 029_EmployeeCoreManagement
    Purpose: Employee directory, core profile CRUD and lifecycle status updates.
    All procedures require authenticated TenantId and explicit CompanyId.
*/
SET NOCOUNT ON;

INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'EMPLOYEE', source.Description, 1, 1
FROM (VALUES
    ('employees.read', N'Read employees', N'View employees within authorized tenant and company scope.'),
    ('employees.create', N'Create employees', N'Create employees within an authorized company.'),
    ('employees.update', N'Update employees', N'Update employee profiles and lifecycle status within an authorized company.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS (SELECT 1 FROM security.Permissions p WHERE p.PermissionCode = source.PermissionCode);
GO

CREATE OR ALTER PROCEDURE hrms.usp_Employees_GetAll
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier,
    @Search nvarchar(100) = NULL, @EmploymentStatus varchar(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT EmployeeId, CompanyId, EmployeeCode, FirstName, MiddleName, LastName,
           PreferredName, WorkEmail, HireDate, EmploymentStatus, TerminationDate,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Employees
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND DeletedAtUtc IS NULL
      AND (@EmploymentStatus IS NULL OR EmploymentStatus = @EmploymentStatus)
      AND (@Search IS NULL OR EmployeeCode LIKE N'%' + @Search + N'%'
           OR FirstName LIKE N'%' + @Search + N'%'
           OR MiddleName LIKE N'%' + @Search + N'%'
           OR LastName LIKE N'%' + @Search + N'%'
           OR PreferredName LIKE N'%' + @Search + N'%'
           OR WorkEmail LIKE N'%' + @Search + N'%')
    ORDER BY LastName, FirstName, EmployeeCode;
END;
GO

CREATE OR ALTER PROCEDURE hrms.usp_Employees_GetById
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @EmployeeId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT EmployeeId, CompanyId, EmployeeCode, FirstName, MiddleName, LastName,
           PreferredName, WorkEmail, HireDate, EmploymentStatus, TerminationDate,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Employees
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND EmployeeId = @EmployeeId AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE hrms.usp_Employees_Create
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier,
    @EmployeeCode nvarchar(30), @FirstName nvarchar(100), @MiddleName nvarchar(100) = NULL,
    @LastName nvarchar(100), @PreferredName nvarchar(100) = NULL,
    @WorkEmail nvarchar(254) = NULL, @HireDate date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM hrms.Companies
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND IsActive = 1 AND DeletedAtUtc IS NULL)
        THROW 51301, 'The company is inactive, unavailable, or outside the authenticated tenant.', 1;

    INSERT INTO hrms.Employees
        (TenantId, CompanyId, EmployeeCode, FirstName, MiddleName, LastName,
         PreferredName, WorkEmail, HireDate)
    OUTPUT INSERTED.EmployeeId, INSERTED.CompanyId, INSERTED.EmployeeCode,
        INSERTED.FirstName, INSERTED.MiddleName, INSERTED.LastName, INSERTED.PreferredName,
        INSERTED.WorkEmail, INSERTED.HireDate, INSERTED.EmploymentStatus, INSERTED.TerminationDate,
        INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    VALUES
        (@TenantId, @CompanyId, @EmployeeCode, @FirstName, @MiddleName, @LastName,
         @PreferredName, @WorkEmail, @HireDate);
END;
GO

CREATE OR ALTER PROCEDURE hrms.usp_Employees_Update
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @EmployeeId uniqueidentifier,
    @FirstName nvarchar(100), @MiddleName nvarchar(100) = NULL, @LastName nvarchar(100),
    @PreferredName nvarchar(100) = NULL, @WorkEmail nvarchar(254) = NULL,
    @HireDate date, @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    UPDATE hrms.Employees
    SET FirstName = @FirstName, MiddleName = @MiddleName, LastName = @LastName,
        PreferredName = @PreferredName, WorkEmail = @WorkEmail, HireDate = @HireDate,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.EmployeeId, INSERTED.CompanyId, INSERTED.EmployeeCode,
        INSERTED.FirstName, INSERTED.MiddleName, INSERTED.LastName, INSERTED.PreferredName,
        INSERTED.WorkEmail, INSERTED.HireDate, INSERTED.EmploymentStatus, INSERTED.TerminationDate,
        INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND EmployeeId = @EmployeeId AND RowVersion = @RowVersion AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE hrms.usp_Employees_UpdateStatus
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @EmployeeId uniqueidentifier,
    @EmploymentStatus varchar(20), @TerminationDate date = NULL, @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    UPDATE hrms.Employees
    SET EmploymentStatus = @EmploymentStatus, TerminationDate = @TerminationDate,
        IsActive = CASE WHEN @EmploymentStatus IN ('Inactive', 'Terminated') THEN 0 ELSE 1 END,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND EmployeeId = @EmployeeId AND RowVersion = @RowVersion AND DeletedAtUtc IS NULL;
    SELECT @@ROWCOUNT;
END;
GO
