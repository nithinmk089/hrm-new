/*
    Migration: 023_LeaveManagerApprovals
    Purpose:
      - List pending requests for the authenticated employee's direct reports.
      - Approve or reject pending requests with manager, tenant and rowversion checks.
      - Convert or release balance reservations atomically with request history.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_GetPendingApprovals]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ManagerEmployeeId uniqueidentifier;

    SELECT @ManagerEmployeeId = mapping.EmployeeId
    FROM hrms.EmployeeUserMappings mapping
    INNER JOIN security.UserTenantMemberships membership
        ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
    INNER JOIN hrms.Employees manager
        ON manager.TenantId = mapping.TenantId AND manager.CompanyId = mapping.CompanyId
       AND manager.EmployeeId = mapping.EmployeeId
    WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
      AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
      AND membership.MembershipStatus = 'Active'
      AND manager.IsActive = 1 AND manager.DeletedAtUtc IS NULL;

    IF @ManagerEmployeeId IS NULL
        RETURN;

    SELECT request.LeaveRequestId, request.EmployeeId, request.LeaveTypeId,
           leaveType.LeaveTypeCode, leaveType.LeaveTypeName,
           request.FromDate, request.ToDate, request.IsHalfDay, request.HalfDayPeriod,
           request.DaysRequested, request.Reason, request.SubmittedAtUtc, request.RowVersion
    FROM hrms.LeaveRequests request
    INNER JOIN hrms.LeaveTypes leaveType
        ON leaveType.TenantId = request.TenantId
       AND leaveType.CompanyId = request.CompanyId
       AND leaveType.LeaveTypeId = request.LeaveTypeId
    WHERE request.TenantId = @TenantId AND request.CompanyId = @CompanyId
      AND request.Status = 'Pending'
      AND EXISTS
      (
          SELECT 1
          FROM hrms.EmployeeReportingAssignments assignment
          WHERE assignment.TenantId = @TenantId AND assignment.CompanyId = @CompanyId
            AND assignment.EmployeeId = request.EmployeeId
            AND assignment.ManagerEmployeeId = @ManagerEmployeeId
            AND assignment.StartDate <= CONVERT(date, SYSUTCDATETIME())
            AND (assignment.EndDate IS NULL OR assignment.EndDate >= CONVERT(date, SYSUTCDATETIME()))
            AND assignment.DeletedAtUtc IS NULL
      )
    ORDER BY request.SubmittedAtUtc, request.LeaveRequestId;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_Decide]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @LeaveRequestId uniqueidentifier,
    @Action varchar(10),
    @RowVersion binary(8),
    @Comments nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Action NOT IN ('Approved', 'Rejected')
        THROW 51290, 'The requested leave decision is invalid.', 1;

    IF @Action = 'Rejected' AND NULLIF(LTRIM(RTRIM(@Comments)), N'') IS NULL
        THROW 51291, 'A reason is required when rejecting a leave request.', 1;

    DECLARE @ManagerEmployeeId uniqueidentifier;
    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @CurrentStatus varchar(20);
    DECLARE @CurrentRowVersion binary(8);
    DECLARE @ReviewedAtUtc datetime2(7) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @ReviewedAtUtc);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @ManagerEmployeeId = mapping.EmployeeId
        FROM hrms.EmployeeUserMappings mapping WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
        INNER JOIN hrms.Employees manager
            ON manager.TenantId = mapping.TenantId AND manager.CompanyId = mapping.CompanyId
           AND manager.EmployeeId = mapping.EmployeeId
        WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
          AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
          AND membership.MembershipStatus = 'Active'
          AND manager.IsActive = 1 AND manager.DeletedAtUtc IS NULL;

        IF @ManagerEmployeeId IS NULL
            THROW 51292, 'The authenticated user is not mapped to an active manager employee.', 1;

        SELECT @EmployeeId = request.EmployeeId,
               @CurrentStatus = request.Status,
               @CurrentRowVersion = request.RowVersion
        FROM hrms.LeaveRequests request WITH (UPDLOCK, HOLDLOCK)
        WHERE request.TenantId = @TenantId AND request.CompanyId = @CompanyId
          AND request.LeaveRequestId = @LeaveRequestId;

        IF @EmployeeId IS NULL
            THROW 51293, 'The leave request was not found in this company.', 1;

        IF @EmployeeId = @ManagerEmployeeId
            THROW 51294, 'A manager cannot approve or reject their own leave request.', 1;

        IF @CurrentStatus <> 'Pending'
            THROW 51295, 'Only pending leave requests can be approved or rejected.', 1;

        IF @CurrentRowVersion <> @RowVersion
            THROW 51296, 'The leave request has changed. Refresh it and try again.', 1;

        IF NOT EXISTS
        (
            SELECT 1
            FROM hrms.EmployeeReportingAssignments assignment
            WHERE assignment.TenantId = @TenantId AND assignment.CompanyId = @CompanyId
              AND assignment.EmployeeId = @EmployeeId
              AND assignment.ManagerEmployeeId = @ManagerEmployeeId
              AND assignment.StartDate <= @Today
              AND (assignment.EndDate IS NULL OR assignment.EndDate >= @Today)
              AND assignment.DeletedAtUtc IS NULL
        )
            THROW 51297, 'Only the employee''s currently assigned reporting manager can decide this request.', 1;

        DECLARE @Reservation TABLE
        (
            EmployeeLeaveBalanceId uniqueidentifier NOT NULL PRIMARY KEY,
            Days decimal(7,2) NOT NULL
        );

        INSERT INTO @Reservation (EmployeeLeaveBalanceId, Days)
        SELECT transactionRow.EmployeeLeaveBalanceId, SUM(transactionRow.DaysDelta)
        FROM hrms.EmployeeLeaveBalanceTransactions transactionRow
        WHERE transactionRow.TenantId = @TenantId AND transactionRow.CompanyId = @CompanyId
          AND transactionRow.LeaveRequestId = @LeaveRequestId
          AND transactionRow.TransactionType = 'Reservation'
        GROUP BY transactionRow.EmployeeLeaveBalanceId;

        IF NOT EXISTS (SELECT 1 FROM @Reservation)
            THROW 51298, 'The pending request has no balance reservation to process.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM @Reservation reservation
            INNER JOIN hrms.EmployeeLeaveBalances balance WITH (UPDLOCK, HOLDLOCK)
                ON balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId
               AND balance.EmployeeLeaveBalanceId = reservation.EmployeeLeaveBalanceId
            WHERE balance.ReservedDays < reservation.Days
        )
            THROW 51298, 'The reserved balance is inconsistent; the request cannot be processed.', 1;

        UPDATE balance
        SET ReservedDays = balance.ReservedDays - reservation.Days,
            UsedDays = balance.UsedDays + CASE WHEN @Action = 'Approved' THEN reservation.Days ELSE 0 END,
            UpdatedAtUtc = @ReviewedAtUtc,
            UpdatedByUserId = @ActorUserId
        FROM hrms.EmployeeLeaveBalances balance
        INNER JOIN @Reservation reservation
            ON reservation.EmployeeLeaveBalanceId = balance.EmployeeLeaveBalanceId
        WHERE balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId;

        INSERT INTO hrms.EmployeeLeaveBalanceTransactions
            (TenantId, CompanyId, EmployeeLeaveBalanceId, LeaveRequestId,
             TransactionType, DaysDelta, Notes, CreatedByUserId)
        SELECT @TenantId, @CompanyId, reservation.EmployeeLeaveBalanceId, @LeaveRequestId,
               'ReservationRelease', -reservation.Days,
               CASE WHEN @Action = 'Approved' THEN N'Approved leave: reservation converted to usage'
                    ELSE N'Rejected leave: reservation released' END,
               @ActorUserId
        FROM @Reservation reservation;

        IF @Action = 'Approved'
        BEGIN
            INSERT INTO hrms.EmployeeLeaveBalanceTransactions
                (TenantId, CompanyId, EmployeeLeaveBalanceId, LeaveRequestId,
                 TransactionType, DaysDelta, Notes, CreatedByUserId)
            SELECT @TenantId, @CompanyId, reservation.EmployeeLeaveBalanceId, @LeaveRequestId,
                   'Usage', reservation.Days, N'Approved leave usage', @ActorUserId
            FROM @Reservation reservation;
        END;

        UPDATE hrms.LeaveRequests
        SET Status = @Action,
            ReviewedByEmployeeId = @ManagerEmployeeId,
            ReviewedAtUtc = @ReviewedAtUtc,
            ReviewComments = @Comments,
            UpdatedAtUtc = @ReviewedAtUtc,
            UpdatedByUserId = @ActorUserId
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND LeaveRequestId = @LeaveRequestId AND Status = 'Pending'
          AND RowVersion = @RowVersion;

        IF @@ROWCOUNT <> 1
            THROW 51296, 'The leave request has changed. Refresh it and try again.', 1;

        INSERT INTO hrms.LeaveRequestApprovals
            (TenantId, CompanyId, LeaveRequestId, [Action], ActorEmployeeId, Comments)
        VALUES
            (@TenantId, @CompanyId, @LeaveRequestId, @Action, @ManagerEmployeeId, @Comments);

        COMMIT TRANSACTION;

        SELECT @LeaveRequestId AS LeaveRequestId,
               @Action AS Status,
               @ManagerEmployeeId AS ReviewedByEmployeeId,
               @ReviewedAtUtc AS ReviewedAtUtc,
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
