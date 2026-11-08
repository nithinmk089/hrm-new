/*
    Migration: 012_DesignationStoredProcedures
    Purpose:
      - Add tenant/company-scoped designation persistence procedures.
      - Validate active company ownership on creation.
      - Preserve optimistic concurrency through RowVersion.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_Designations_GetAll]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DesignationId, CompanyId, DesignationCode, DesignationName, Description,
           JobLevel, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Designations
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DeletedAtUtc IS NULL
    ORDER BY JobLevel, DesignationName, DesignationCode;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Designations_GetById]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DesignationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DesignationId, CompanyId, DesignationCode, DesignationName, Description,
           JobLevel, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Designations
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DesignationId = @DesignationId
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Designations_GetByCode]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DesignationCode nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DesignationId, CompanyId, DesignationCode, DesignationName, Description,
           JobLevel, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Designations
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DesignationCode = @DesignationCode
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Designations_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DesignationCode nvarchar(30),
    @DesignationName nvarchar(150),
    @Description nvarchar(500) = NULL,
    @JobLevel smallint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM hrms.Companies
        WHERE TenantId = @TenantId
          AND CompanyId = @CompanyId
          AND IsActive = 1
          AND DeletedAtUtc IS NULL
    )
        THROW 51221, 'The company does not exist, is inactive, or is outside the authenticated tenant.', 1;

    INSERT INTO hrms.Designations
        (TenantId, CompanyId, DesignationCode, DesignationName, Description, JobLevel)
    OUTPUT INSERTED.DesignationId, INSERTED.CompanyId, INSERTED.DesignationCode,
           INSERTED.DesignationName, INSERTED.Description, INSERTED.JobLevel,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    VALUES
        (@TenantId, @CompanyId, @DesignationCode, @DesignationName, @Description, @JobLevel);
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Designations_Update]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DesignationId uniqueidentifier,
    @DesignationName nvarchar(150),
    @Description nvarchar(500) = NULL,
    @JobLevel smallint = NULL,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Designations
    SET DesignationName = @DesignationName,
        Description = @Description,
        JobLevel = @JobLevel,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.DesignationId, INSERTED.CompanyId, INSERTED.DesignationCode,
           INSERTED.DesignationName, INSERTED.Description, INSERTED.JobLevel,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DesignationId = @DesignationId
      AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Designations_Deactivate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DesignationId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Designations
    SET IsActive = 0,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DesignationId = @DesignationId
      AND RowVersion = @RowVersion
      AND IsActive = 1
      AND DeletedAtUtc IS NULL;

    SELECT @@ROWCOUNT;
END;
GO
