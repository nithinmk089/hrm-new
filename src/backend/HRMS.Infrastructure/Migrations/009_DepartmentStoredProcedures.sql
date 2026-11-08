/*
    Migration: 009_DepartmentStoredProcedures
    Purpose:
      - Add tenant- and company-scoped department persistence procedures.
      - Enforce active company/parent validation and prevent hierarchy cycles.
      - Keep optimistic concurrency through the existing rowversion column.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_GetAll]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DepartmentId, CompanyId, DepartmentCode, DepartmentName, Description,
           ParentDepartmentId, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Departments
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DeletedAtUtc IS NULL
    ORDER BY DepartmentName, DepartmentCode;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_GetById]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DepartmentId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DepartmentId, CompanyId, DepartmentCode, DepartmentName, Description,
           ParentDepartmentId, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Departments
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DepartmentId = @DepartmentId
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_GetByCode]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DepartmentCode nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DepartmentId, CompanyId, DepartmentCode, DepartmentName, Description,
           ParentDepartmentId, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Departments
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DepartmentCode = @DepartmentCode
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_ParentExists]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ParentDepartmentId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(CASE WHEN EXISTS
    (
        SELECT 1
        FROM hrms.Departments
        WHERE TenantId = @TenantId
          AND CompanyId = @CompanyId
          AND DepartmentId = @ParentDepartmentId
          AND IsActive = 1
          AND DeletedAtUtc IS NULL
    ) THEN 1 ELSE 0 END AS bit);
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DepartmentCode nvarchar(30),
    @DepartmentName nvarchar(150),
    @Description nvarchar(500) = NULL,
    @ParentDepartmentId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.Companies
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51201, 'The company does not exist, is inactive, or is outside the authenticated tenant.', 1;

    IF @ParentDepartmentId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM hrms.Departments
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND DepartmentId = @ParentDepartmentId
          AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51202, 'The parent department must be active and belong to the same company.', 1;

    INSERT INTO hrms.Departments
        (TenantId, CompanyId, DepartmentCode, DepartmentName, Description, ParentDepartmentId)
    OUTPUT INSERTED.DepartmentId, INSERTED.CompanyId, INSERTED.DepartmentCode,
           INSERTED.DepartmentName, INSERTED.Description, INSERTED.ParentDepartmentId,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    VALUES
        (@TenantId, @CompanyId, @DepartmentCode, @DepartmentName, @Description, @ParentDepartmentId);
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_Update]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DepartmentId uniqueidentifier,
    @DepartmentName nvarchar(150),
    @Description nvarchar(500) = NULL,
    @ParentDepartmentId uniqueidentifier = NULL,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ParentDepartmentId = @DepartmentId
        THROW 51203, 'A department cannot be its own parent.', 1;

    IF @ParentDepartmentId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM hrms.Departments
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND DepartmentId = @ParentDepartmentId
          AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51202, 'The parent department must be active and belong to the same company.', 1;

    DECLARE @ParentIsDescendant bit = 0;

    ;WITH DepartmentDescendants AS
    (
        SELECT DepartmentId
        FROM hrms.Departments
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND DepartmentId = @DepartmentId AND DeletedAtUtc IS NULL

        UNION ALL

        SELECT child.DepartmentId
        FROM hrms.Departments AS child
        INNER JOIN DepartmentDescendants AS parent
            ON child.ParentDepartmentId = parent.DepartmentId
        WHERE child.TenantId = @TenantId AND child.CompanyId = @CompanyId
          AND child.DeletedAtUtc IS NULL
    )
    SELECT @ParentIsDescendant = CASE WHEN EXISTS
    (
        SELECT 1 FROM DepartmentDescendants WHERE DepartmentId = @ParentDepartmentId
    ) THEN 1 ELSE 0 END
    OPTION (MAXRECURSION 100);

    IF @ParentIsDescendant = 1
        THROW 51204, 'The selected parent would create a department hierarchy cycle.', 1;

    UPDATE hrms.Departments
    SET DepartmentName = @DepartmentName,
        Description = @Description,
        ParentDepartmentId = @ParentDepartmentId,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.DepartmentId, INSERTED.CompanyId, INSERTED.DepartmentCode,
           INSERTED.DepartmentName, INSERTED.Description, INSERTED.ParentDepartmentId,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DepartmentId = @DepartmentId
      AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_Deactivate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DepartmentId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Departments
    SET IsActive = 0,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DepartmentId = @DepartmentId
      AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;
    
    SELECT @@ROWCOUNT;
END;
GO
