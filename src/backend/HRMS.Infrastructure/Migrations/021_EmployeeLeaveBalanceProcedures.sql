/*
    Migration: 021_EmployeeLeaveBalanceProcedures
    Purpose:
      - Add explicit adjustment days to annual leave balances.
      - Read annual balances and apply audited balance transactions atomically.
*/
ALTER TABLE [hrms].[EmployeeLeaveBalances]
    ADD [AdjustmentDays] decimal(7,2) NOT NULL
        CONSTRAINT [DF_EmployeeLeaveBalances_Adjustment] DEFAULT (0);
GO

ALTER TABLE [hrms].[EmployeeLeaveBalances]
    DROP CONSTRAINT [CK_EmployeeLeaveBalances_NotOverdrawn];
GO
ALTER TABLE [hrms].[EmployeeLeaveBalances]
    ADD CONSTRAINT [CK_EmployeeLeaveBalances_NotOverdrawn]
        CHECK ([UsedDays] + [ReservedDays] <=
            [OpeningDays] + [AccruedDays] + [CarriedForwardDays] + [AdjustmentDays]);
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_EmployeeLeaveBalances_GetAll]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @EmployeeId uniqueidentifier,
    @LeaveYear smallint
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.EmployeeLeaveBalanceId, b.CompanyId, b.EmployeeId, b.LeaveTypeId,
           t.LeaveTypeCode, t.LeaveTypeName, b.LeaveYear,
           b.OpeningDays, b.AccruedDays, b.CarriedForwardDays, b.AdjustmentDays,
           b.UsedDays, b.ReservedDays,
           (b.OpeningDays + b.AccruedDays + b.CarriedForwardDays + b.AdjustmentDays
                - b.UsedDays - b.ReservedDays) AS AvailableDays,
           b.CreatedAtUtc, b.UpdatedAtUtc, b.RowVersion
    FROM hrms.EmployeeLeaveBalances b
    INNER JOIN hrms.LeaveTypes t
        ON t.TenantId = b.TenantId AND t.CompanyId = b.CompanyId AND t.LeaveTypeId = b.LeaveTypeId
    WHERE b.TenantId = @TenantId AND b.CompanyId = @CompanyId
      AND b.EmployeeId = @EmployeeId AND b.LeaveYear = @LeaveYear
    ORDER BY t.LeaveTypeName;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_EmployeeLeaveBalances_ApplyTransaction]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @EmployeeId uniqueidentifier,
    @LeaveTypeId uniqueidentifier,
    @LeaveYear smallint,
    @TransactionType varchar(20),
    @DaysDelta decimal(7,2),
    @Notes nvarchar(500) = NULL,
    @ActorUserId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @DaysDelta = 0
        THROW 51270, 'Balance transaction amount cannot be zero.', 1;

    IF @TransactionType NOT IN ('Opening','Accrual','CarryForward','Adjustment')
        THROW 51270, 'Only opening, accrual, carry-forward and adjustment transactions are supported here.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.Employees
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND EmployeeId = @EmployeeId AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51271, 'The employee does not exist, is inactive, or is outside the authenticated company.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.LeaveTypes
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND LeaveTypeId = @LeaveTypeId AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51272, 'The leave type does not exist, is inactive, or is outside the authenticated company.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @BalanceId uniqueidentifier;
        SELECT @BalanceId = EmployeeLeaveBalanceId
        FROM hrms.EmployeeLeaveBalances WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND EmployeeId = @EmployeeId AND LeaveTypeId = @LeaveTypeId AND LeaveYear = @LeaveYear;

        IF @BalanceId IS NULL
        BEGIN
            IF @DaysDelta < 0
                THROW 51273, 'A balance must be initialized with a positive transaction before it can be reduced.', 1;

            INSERT INTO hrms.EmployeeLeaveBalances
                (TenantId, CompanyId, EmployeeId, LeaveTypeId, LeaveYear)
            VALUES (@TenantId, @CompanyId, @EmployeeId, @LeaveTypeId, @LeaveYear);

            SET @BalanceId = (SELECT EmployeeLeaveBalanceId FROM hrms.EmployeeLeaveBalances
                WHERE TenantId = @TenantId AND CompanyId = @CompanyId
                  AND EmployeeId = @EmployeeId AND LeaveTypeId = @LeaveTypeId AND LeaveYear = @LeaveYear);
        END;

        IF @TransactionType = 'Opening'
            UPDATE hrms.EmployeeLeaveBalances SET OpeningDays = OpeningDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE EmployeeLeaveBalanceId = @BalanceId;
        ELSE IF @TransactionType = 'Accrual'
            UPDATE hrms.EmployeeLeaveBalances SET AccruedDays = AccruedDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE EmployeeLeaveBalanceId = @BalanceId;
        ELSE IF @TransactionType = 'CarryForward'
            UPDATE hrms.EmployeeLeaveBalances SET CarriedForwardDays = CarriedForwardDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE EmployeeLeaveBalanceId = @BalanceId;
        ELSE
            UPDATE hrms.EmployeeLeaveBalances SET AdjustmentDays = AdjustmentDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE EmployeeLeaveBalanceId = @BalanceId;

        INSERT INTO hrms.EmployeeLeaveBalanceTransactions
            (TenantId, CompanyId, EmployeeLeaveBalanceId, TransactionType, DaysDelta, Notes, CreatedByUserId)
        VALUES (@TenantId, @CompanyId, @BalanceId, @TransactionType, @DaysDelta, @Notes, @ActorUserId);

        COMMIT TRANSACTION;

        SELECT b.EmployeeLeaveBalanceId, b.CompanyId, b.EmployeeId, b.LeaveTypeId,
               t.LeaveTypeCode, t.LeaveTypeName, b.LeaveYear,
               b.OpeningDays, b.AccruedDays, b.CarriedForwardDays, b.AdjustmentDays,
               b.UsedDays, b.ReservedDays,
               (b.OpeningDays + b.AccruedDays + b.CarriedForwardDays + b.AdjustmentDays
                    - b.UsedDays - b.ReservedDays) AS AvailableDays,
               b.CreatedAtUtc, b.UpdatedAtUtc, b.RowVersion
        FROM hrms.EmployeeLeaveBalances b
        INNER JOIN hrms.LeaveTypes t
            ON t.TenantId = b.TenantId AND t.CompanyId = b.CompanyId AND t.LeaveTypeId = b.LeaveTypeId
        WHERE b.EmployeeLeaveBalanceId = @BalanceId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
