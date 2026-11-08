/*
    Migration: 024_LeaveRequestHistoryAndCancellation
    Purpose:
      - Read the authenticated employee's leave requests and action history.
      - Allow requester cancellation with reservation release or usage restoration.
      - Keep request, balances, ledger and history atomic.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_GetMine]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EmployeeId uniqueidentifier;

    SELECT @EmployeeId = employee.EmployeeId
    FROM hrms.EmployeeUserMappings mapping
    INNER JOIN security.UserTenantMemberships membership
        ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
    INNER JOIN hrms.Employees employee
        ON employee.TenantId = mapping.TenantId AND employee.CompanyId = mapping.CompanyId
       AND employee.EmployeeId = mapping.EmployeeId
    INNER JOIN hrms.Companies company
        ON company.TenantId = mapping.TenantId AND company.CompanyId = mapping.CompanyId
    WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
      AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
      AND membership.MembershipStatus = 'Active'
      AND employee.IsActive = 1 AND employee.DeletedAtUtc IS NULL
      AND company.IsActive = 1 AND company.DeletedAtUtc IS NULL;

    IF @EmployeeId IS NULL
        RETURN;

    SELECT TOP (100)
           request.LeaveRequestId, request.CompanyId, request.EmployeeId, request.LeaveTypeId,
           leaveType.LeaveTypeCode, leaveType.LeaveTypeName,
           request.FromDate, request.ToDate, request.IsHalfDay, request.HalfDayPeriod,
           request.DaysRequested, request.Reason, request.Status, request.SubmittedAtUtc,
           request.ReviewedByEmployeeId, request.ReviewedAtUtc, request.ReviewComments,
           request.CancelledAtUtc, request.RowVersion
    FROM hrms.LeaveRequests request
    INNER JOIN hrms.LeaveTypes leaveType
        ON leaveType.TenantId = request.TenantId AND leaveType.CompanyId = request.CompanyId
       AND leaveType.LeaveTypeId = request.LeaveTypeId
    WHERE request.TenantId = @TenantId AND request.CompanyId = @CompanyId
      AND request.EmployeeId = @EmployeeId
    ORDER BY request.SubmittedAtUtc DESC, request.LeaveRequestId DESC;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_GetMyHistory]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @LeaveRequestId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT history.[Action], history.ActorEmployeeId, history.Comments, history.ActionAtUtc
    FROM hrms.LeaveRequestApprovals history
    INNER JOIN hrms.LeaveRequests request
        ON request.TenantId = history.TenantId AND request.CompanyId = history.CompanyId
       AND request.LeaveRequestId = history.LeaveRequestId
    INNER JOIN hrms.EmployeeUserMappings mapping
        ON mapping.TenantId = request.TenantId AND mapping.CompanyId = request.CompanyId
       AND mapping.EmployeeId = request.EmployeeId
    INNER JOIN security.UserTenantMemberships membership
        ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
    WHERE request.TenantId = @TenantId AND request.CompanyId = @CompanyId
      AND request.LeaveRequestId = @LeaveRequestId
      AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
      AND membership.MembershipStatus = 'Active'
    ORDER BY history.ActionAtUtc, history.LeaveRequestApprovalId;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_Cancel]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @LeaveRequestId uniqueidentifier,
    @RowVersion binary(8),
    @Comments nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @Status varchar(20);
    DECLARE @CurrentRowVersion binary(8);
    DECLARE @FromDate date;
    DECLARE @CancelledAtUtc datetime2(7) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @CancelledAtUtc);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EmployeeId = employee.EmployeeId
        FROM hrms.EmployeeUserMappings mapping WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
        INNER JOIN hrms.Employees employee
            ON employee.TenantId = mapping.TenantId AND employee.CompanyId = mapping.CompanyId
           AND employee.EmployeeId = mapping.EmployeeId
        INNER JOIN hrms.Companies company
            ON company.TenantId = mapping.TenantId AND company.CompanyId = mapping.CompanyId
        WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
          AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
          AND membership.MembershipStatus = 'Active'
          AND employee.IsActive = 1 AND employee.DeletedAtUtc IS NULL
          AND company.IsActive = 1 AND company.DeletedAtUtc IS NULL;

        IF @EmployeeId IS NULL
            THROW 51300, 'The authenticated user is not mapped to an active employee in this company.', 1;

        SELECT @Status = request.Status,
               @CurrentRowVersion = request.RowVersion,
               @FromDate = request.FromDate
        FROM hrms.LeaveRequests request WITH (UPDLOCK, HOLDLOCK)
        WHERE request.TenantId = @TenantId AND request.CompanyId = @CompanyId
          AND request.EmployeeId = @EmployeeId
          AND request.LeaveRequestId = @LeaveRequestId;

        IF @Status IS NULL
            THROW 51301, 'The leave request was not found for the authenticated employee.', 1;

        IF @Status NOT IN ('Pending', 'Approved')
            THROW 51302, 'Only pending or approved leave requests can be cancelled.', 1;

        IF @Status = 'Approved' AND @FromDate <= @Today
            THROW 51303, 'An approved leave request can only be cancelled before its start date.', 1;

        IF @CurrentRowVersion <> @RowVersion
            THROW 51304, 'The leave request has changed. Refresh it and try again.', 1;

        DECLARE @Adjustments TABLE
        (
            EmployeeLeaveBalanceId uniqueidentifier NOT NULL PRIMARY KEY,
            Days decimal(7,2) NOT NULL
        );

        IF @Status = 'Pending'
        BEGIN
            INSERT INTO @Adjustments (EmployeeLeaveBalanceId, Days)
            SELECT transactionRow.EmployeeLeaveBalanceId, SUM(transactionRow.DaysDelta)
            FROM hrms.EmployeeLeaveBalanceTransactions transactionRow
            WHERE transactionRow.TenantId = @TenantId AND transactionRow.CompanyId = @CompanyId
              AND transactionRow.LeaveRequestId = @LeaveRequestId
              AND transactionRow.TransactionType = 'Reservation'
            GROUP BY transactionRow.EmployeeLeaveBalanceId;
        END
        ELSE
        BEGIN
            INSERT INTO @Adjustments (EmployeeLeaveBalanceId, Days)
            SELECT transactionRow.EmployeeLeaveBalanceId, SUM(transactionRow.DaysDelta)
            FROM hrms.EmployeeLeaveBalanceTransactions transactionRow
            WHERE transactionRow.TenantId = @TenantId AND transactionRow.CompanyId = @CompanyId
              AND transactionRow.LeaveRequestId = @LeaveRequestId
              AND transactionRow.TransactionType = 'Usage'
            GROUP BY transactionRow.EmployeeLeaveBalanceId;
        END;

        IF NOT EXISTS (SELECT 1 FROM @Adjustments)
            THROW 51305, 'The leave request has no corresponding balance ledger entries.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM @Adjustments adjustment
            INNER JOIN hrms.EmployeeLeaveBalances balance WITH (UPDLOCK, HOLDLOCK)
                ON balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId
               AND balance.EmployeeLeaveBalanceId = adjustment.EmployeeLeaveBalanceId
            WHERE (@Status = 'Pending' AND balance.ReservedDays < adjustment.Days)
               OR (@Status = 'Approved' AND balance.UsedDays < adjustment.Days)
        )
            THROW 51305, 'The leave balance does not contain the expected reserved or used days.', 1;

        UPDATE balance
        SET ReservedDays = balance.ReservedDays - CASE WHEN @Status = 'Pending' THEN adjustment.Days ELSE 0 END,
            UsedDays = balance.UsedDays - CASE WHEN @Status = 'Approved' THEN adjustment.Days ELSE 0 END,
            UpdatedAtUtc = @CancelledAtUtc,
            UpdatedByUserId = @ActorUserId
        FROM hrms.EmployeeLeaveBalances balance
        INNER JOIN @Adjustments adjustment
            ON adjustment.EmployeeLeaveBalanceId = balance.EmployeeLeaveBalanceId
        WHERE balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId;

        INSERT INTO hrms.EmployeeLeaveBalanceTransactions
            (TenantId, CompanyId, EmployeeLeaveBalanceId, LeaveRequestId,
             TransactionType, DaysDelta, Notes, CreatedByUserId)
        SELECT @TenantId, @CompanyId, adjustment.EmployeeLeaveBalanceId, @LeaveRequestId,
               CASE WHEN @Status = 'Pending' THEN 'ReservationRelease' ELSE 'Restoration' END,
               -adjustment.Days,
               CASE WHEN @Status = 'Pending' THEN N'Cancelled pending leave: reservation released'
                    ELSE N'Cancelled approved leave: used balance restored' END,
               @ActorUserId
        FROM @Adjustments adjustment;

        UPDATE hrms.LeaveRequests
        SET Status = 'Cancelled',
            CancelledAtUtc = @CancelledAtUtc,
            UpdatedAtUtc = @CancelledAtUtc,
            UpdatedByUserId = @ActorUserId
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND LeaveRequestId = @LeaveRequestId
          AND Status = @Status AND RowVersion = @RowVersion;

        IF @@ROWCOUNT <> 1
            THROW 51304, 'The leave request has changed. Refresh it and try again.', 1;

        INSERT INTO hrms.LeaveRequestApprovals
            (TenantId, CompanyId, LeaveRequestId, [Action], ActorEmployeeId, Comments)
        VALUES
            (@TenantId, @CompanyId, @LeaveRequestId, 'Cancelled', @EmployeeId, @Comments);

        COMMIT TRANSACTION;

        SELECT @LeaveRequestId AS LeaveRequestId,
               CAST('Cancelled' AS varchar(20)) AS Status,
               @CancelledAtUtc AS CancelledAtUtc,
               RowVersion
        FROM hrms.LeaveRequests
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND LeaveRequestId = @LeaveRequestId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
