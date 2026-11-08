/*
    Migration: 028_DepartmentActivation
    Purpose: Allow authorized users to reactivate a department using optimistic concurrency.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_Departments_Activate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @DepartmentId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Departments
    SET IsActive = 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DepartmentId = @DepartmentId
      AND IsActive = 0
      AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;

    SELECT @@ROWCOUNT;
END;
GO
