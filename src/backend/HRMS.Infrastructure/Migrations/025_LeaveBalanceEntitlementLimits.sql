/*
    Migration: 025_LeaveBalanceEntitlementLimits
    Purpose:
      - Enforce configured annual entitlement and carry-forward caps for employee balance transactions.
      - Keep the checks inside the serialized SQL transaction so concurrent grants cannot exceed policy.
*/
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

    IF @TransactionType <> 'Adjustment' AND @DaysDelta < 0
        THROW 51275, 'Negative balance transactions are only allowed for Adjustment.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.Employees
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND EmployeeId = @EmployeeId AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51271, 'The employee does not exist, is inactive, or is outside the authenticated company.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @AnnualEntitlementDays decimal(7,2);
        DECLARE @AllowCarryForward bit;
        DECLARE @MaxCarryForwardDays decimal(7,2);
        DECLARE @LeaveTypeFound bit = 0;

        SELECT @AnnualEntitlementDays = AnnualEntitlementDays,
               @AllowCarryForward = AllowCarryForward,
               @MaxCarryForwardDays = MaxCarryForwardDays,
               @LeaveTypeFound = 1
        FROM hrms.LeaveTypes WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND LeaveTypeId = @LeaveTypeId AND IsActive = 1 AND DeletedAtUtc IS NULL;

        IF @LeaveTypeFound = 0
            THROW 51272, 'The leave type does not exist, is inactive, or is outside the authenticated company.', 1;

        DECLARE @BalanceId uniqueidentifier;
        DECLARE @OpeningDays decimal(7,2) = 0;
        DECLARE @AccruedDays decimal(7,2) = 0;
        DECLARE @CarriedForwardDays decimal(7,2) = 0;
        DECLARE @AdjustmentDays decimal(7,2) = 0;
        DECLARE @UsedDays decimal(7,2) = 0;
        DECLARE @ReservedDays decimal(7,2) = 0;

        SELECT @BalanceId = EmployeeLeaveBalanceId,
               @OpeningDays = OpeningDays,
               @AccruedDays = AccruedDays,
               @CarriedForwardDays = CarriedForwardDays,
               @AdjustmentDays = AdjustmentDays,
               @UsedDays = UsedDays,
               @ReservedDays = ReservedDays
        FROM hrms.EmployeeLeaveBalances WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND EmployeeId = @EmployeeId AND LeaveTypeId = @LeaveTypeId AND LeaveYear = @LeaveYear;

        IF @BalanceId IS NULL AND @DaysDelta < 0
            THROW 51273, 'A balance must be initialized with a positive transaction before it can be reduced.', 1;

        IF @TransactionType = 'CarryForward'
        BEGIN
            IF @DaysDelta > 0 AND @AllowCarryForward = 0
                THROW 51276, 'Carry forward is not enabled for this leave type.', 1;

            IF @CarriedForwardDays + @DaysDelta < 0
                THROW 51277, 'The transaction cannot reduce carried-forward days below zero.', 1;

            IF @AllowCarryForward = 1 AND @CarriedForwardDays + @DaysDelta > @MaxCarryForwardDays
                THROW 51278, 'The transaction exceeds the maximum carry-forward days configured for this leave type.', 1;
        END
        ELSE
        BEGIN
            DECLARE @CurrentAnnualDays decimal(7,2) = @OpeningDays + @AccruedDays + @AdjustmentDays;
            IF @CurrentAnnualDays + @DaysDelta < 0
                THROW 51277, 'The transaction cannot reduce annual entitlement days below zero.', 1;

            IF @AnnualEntitlementDays IS NOT NULL
               AND @CurrentAnnualDays + @DaysDelta > @AnnualEntitlementDays
                THROW 51274, 'The transaction exceeds the annual entitlement configured for this leave type.', 1;

            IF @CurrentAnnualDays + @DaysDelta < @UsedDays + @ReservedDays
                THROW 51279, 'The transaction cannot reduce entitlement below used and reserved days.', 1;
        END;

        IF @BalanceId IS NULL
        BEGIN
            INSERT INTO hrms.EmployeeLeaveBalances
                (TenantId, CompanyId, EmployeeId, LeaveTypeId, LeaveYear, CreatedByUserId)
            VALUES (@TenantId, @CompanyId, @EmployeeId, @LeaveTypeId, @LeaveYear, @ActorUserId);

            SET @BalanceId = (SELECT EmployeeLeaveBalanceId FROM hrms.EmployeeLeaveBalances
                WHERE TenantId = @TenantId AND CompanyId = @CompanyId
                  AND EmployeeId = @EmployeeId AND LeaveTypeId = @LeaveTypeId AND LeaveYear = @LeaveYear);
        END;

        IF @TransactionType = 'Opening'
            UPDATE hrms.EmployeeLeaveBalances SET OpeningDays = OpeningDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
            WHERE EmployeeLeaveBalanceId = @BalanceId;
        ELSE IF @TransactionType = 'Accrual'
            UPDATE hrms.EmployeeLeaveBalances SET AccruedDays = AccruedDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
            WHERE EmployeeLeaveBalanceId = @BalanceId;
        ELSE IF @TransactionType = 'CarryForward'
            UPDATE hrms.EmployeeLeaveBalances SET CarriedForwardDays = CarriedForwardDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
            WHERE EmployeeLeaveBalanceId = @BalanceId;
        ELSE
            UPDATE hrms.EmployeeLeaveBalances SET AdjustmentDays = AdjustmentDays + @DaysDelta,
                UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @ActorUserId
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
