/*
    Migration: 020_EmployeeUserMappingProcedures
    Purpose:
      - Manage explicit employee-to-user mappings.
      - Require an active employee and active tenant membership.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_EmployeeUserMappings_GetByEmployee]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @EmployeeId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT EmployeeUserMappingId, CompanyId, EmployeeId, UserId, IsActive,
           CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.EmployeeUserMappings
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND EmployeeId = @EmployeeId AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_EmployeeUserMappings_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @EmployeeId uniqueidentifier,
    @UserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.Employees
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND EmployeeId = @EmployeeId AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51261, 'The employee does not exist, is inactive, or is outside the authenticated company.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM security.UserTenantMemberships
        WHERE TenantId = @TenantId AND UserId = @UserId AND MembershipStatus = 'Active'
    )
        THROW 51262, 'The user does not have an active membership in the authenticated tenant.', 1;

    INSERT INTO hrms.EmployeeUserMappings
        (TenantId, CompanyId, EmployeeId, UserId)
    OUTPUT INSERTED.EmployeeUserMappingId, INSERTED.CompanyId,
           INSERTED.EmployeeId, INSERTED.UserId, INSERTED.IsActive,
           INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    VALUES (@TenantId, @CompanyId, @EmployeeId, @UserId);
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_EmployeeUserMappings_Deactivate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @EmployeeUserMappingId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.EmployeeUserMappings
    SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND EmployeeUserMappingId = @EmployeeUserMappingId
      AND IsActive = 1 AND RowVersion = @RowVersion;

    SELECT @@ROWCOUNT;
END;
GO
