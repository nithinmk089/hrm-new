/*
    Migration: 022_LeaveRequestSubmission
    Purpose:
      - Resolve authenticated employee and leave calendar context.
      - Submit leave requests with overlap protection.
      - Reserve or consume annual balances atomically across calendar years.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_GetSubmissionContext]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @LeaveTypeId uniqueidentifier,
    @FromDate date,
    @ToDate date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT e.EmployeeId, lt.LeaveTypeId, lt.LeaveTypeCode, lt.LeaveTypeName,
           lt.RequiresApproval, lt.AllowHalfDay
    FROM hrms.EmployeeUserMappings m
    INNER JOIN hrms.Employees e
        ON e.TenantId = m.TenantId AND e.CompanyId = m.CompanyId AND e.EmployeeId = m.EmployeeId
    INNER JOIN security.UserTenantMemberships membership
        ON membership.TenantId = m.TenantId AND membership.UserId = m.UserId
    INNER JOIN hrms.LeaveTypes lt
        ON lt.TenantId = m.TenantId AND lt.CompanyId = m.CompanyId
    INNER JOIN hrms.Companies company
        ON company.TenantId = m.TenantId AND company.CompanyId = m.CompanyId
    WHERE company.IsActive = 1 AND company.DeletedAtUtc IS NULL
      AND m.TenantId = @TenantId AND m.CompanyId = @CompanyId
      AND m.UserId = @ActorUserId AND m.IsActive = 1
      AND membership.MembershipStatus = 'Active'
      AND e.IsActive = 1 AND e.DeletedAtUtc IS NULL
      AND lt.LeaveTypeId = @LeaveTypeId AND lt.IsActive = 1 AND lt.DeletedAtUtc IS NULL;

    SELECT DayOfWeek, IsWorkingDay
    FROM hrms.WorkingWeekDays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND ScopeType = 'Company' AND OfficeId IS NULL
    ORDER BY DayOfWeek;

    SELECT StartDate, EndDate, OfficeId
    FROM hrms.EmployeeEmployment
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND EmployeeId =
      (
          SELECT TOP (1) EmployeeId FROM hrms.EmployeeUserMappings
          WHERE TenantId = @TenantId AND CompanyId = @CompanyId
            AND UserId = @ActorUserId AND IsActive = 1
      )
      AND IsPrimary = 1 AND EmploymentStatus IN ('Active','OnLeave')
      AND DeletedAtUtc IS NULL
      AND StartDate <= @ToDate AND (EndDate IS NULL OR EndDate >= @FromDate)
    ORDER BY StartDate;

    SELECT w.OfficeId, w.DayOfWeek, w.IsWorkingDay
    FROM hrms.WorkingWeekDays w
    INNER JOIN
    (
        SELECT OfficeId
        FROM hrms.WorkingWeekDays
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND ScopeType = 'Office' AND OfficeId IS NOT NULL
        GROUP BY OfficeId
        HAVING COUNT(*) = 7
    ) complete ON complete.OfficeId = w.OfficeId
    WHERE w.TenantId = @TenantId AND w.CompanyId = @CompanyId
      AND w.ScopeType = 'Office'
    ORDER BY w.OfficeId, w.DayOfWeek;

    SELECT HolidayDate AS [Date], IsOptional, IsActive
    FROM hrms.Holidays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND HolidayDate BETWEEN @FromDate AND @ToDate
      AND DeletedAtUtc IS NULL
    ORDER BY HolidayDate;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveRequests_Submit]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @LeaveTypeId uniqueidentifier,
    @FromDate date,
    @ToDate date,
    @IsHalfDay bit,
    @HalfDayPeriod varchar(2) = NULL,
    @DaysRequested decimal(7,2),
    @Reason nvarchar(1000),
    @YearDaysJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ToDate < @FromDate OR @DaysRequested <= 0
        THROW 51280, 'The leave date range or calculated leave days are invalid.', 1;

    IF ISJSON(@YearDaysJson) <> 1
        THROW 51281, 'The yearly leave allocation payload is invalid.', 1;

    DECLARE @Allocations TABLE
    (
        LeaveYear smallint NOT NULL PRIMARY KEY,
        Days decimal(7,2) NOT NULL CHECK (Days > 0)
    );

    INSERT INTO @Allocations (LeaveYear, Days)
    SELECT LeaveYear, Days
    FROM OPENJSON(@YearDaysJson)
    WITH
    (
        LeaveYear smallint '$.leaveYear',
        Days decimal(7,2) '$.days'
    );

    IF NOT EXISTS (SELECT 1 FROM @Allocations)
       OR EXISTS (SELECT 1 FROM @Allocations WHERE LeaveYear NOT BETWEEN 2000 AND 2100)
       OR ABS((SELECT SUM(Days) FROM @Allocations) - @DaysRequested) > 0.001
        THROW 51282, 'Yearly balance allocations do not match the calculated leave days.', 1;

    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @RequiresApproval bit;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EmployeeId = e.EmployeeId
        FROM hrms.EmployeeUserMappings m WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN hrms.Companies company
            ON company.TenantId = m.TenantId AND company.CompanyId = m.CompanyId
        INNER JOIN hrms.Employees e
            ON e.TenantId = m.TenantId AND e.CompanyId = m.CompanyId AND e.EmployeeId = m.EmployeeId
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = m.TenantId AND membership.UserId = m.UserId
        WHERE m.TenantId = @TenantId AND m.CompanyId = @CompanyId
          AND m.UserId = @ActorUserId AND m.IsActive = 1
          AND membership.MembershipStatus = 'Active'
          AND company.IsActive = 1 AND company.DeletedAtUtc IS NULL
          AND e.IsActive = 1 AND e.DeletedAtUtc IS NULL;

        IF @EmployeeId IS NULL
            THROW 51283, 'The authenticated user is not mapped to an active employee in this company.', 1;

        SELECT @RequiresApproval = RequiresApproval
        FROM hrms.LeaveTypes WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND LeaveTypeId = @LeaveTypeId AND IsActive = 1 AND DeletedAtUtc IS NULL;

        IF @RequiresApproval IS NULL
            THROW 51284, 'The leave type does not exist, is inactive, or is outside the authenticated company.', 1;

        IF @IsHalfDay = 1 AND (@FromDate <> @ToDate OR @HalfDayPeriod NOT IN ('AM','PM'))
            THROW 51285, 'A half-day request must cover one date and specify AM or PM.', 1;

        IF @IsHalfDay = 0 AND @HalfDayPeriod IS NOT NULL
            THROW 51285, 'HalfDayPeriod must be null for a full-day request.', 1;

        IF @RequiresApproval = 1 AND NOT EXISTS
        (
            SELECT 1
            FROM hrms.EmployeeReportingAssignments assignment
            INNER JOIN hrms.Employees manager
                ON manager.TenantId = assignment.TenantId
               AND manager.CompanyId = assignment.CompanyId
               AND manager.EmployeeId = assignment.ManagerEmployeeId
            WHERE assignment.TenantId = @TenantId
              AND assignment.CompanyId = @CompanyId
              AND assignment.EmployeeId = @EmployeeId
              AND assignment.StartDate <= CONVERT(date, SYSUTCDATETIME())
              AND (assignment.EndDate IS NULL OR assignment.EndDate >= CONVERT(date, SYSUTCDATETIME()))
              AND assignment.DeletedAtUtc IS NULL
              AND manager.IsActive = 1 AND manager.DeletedAtUtc IS NULL
        )
            THROW 51286, 'An active reporting manager must be assigned before submitting a leave type that requires approval.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM hrms.LeaveRequests WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND EmployeeId = @EmployeeId
              AND Status IN ('Pending','Approved')
              AND FromDate <= @ToDate AND ToDate >= @FromDate
        )
            THROW 51287, 'The requested dates overlap an existing pending or approved leave request.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM @Allocations allocation
            LEFT JOIN hrms.EmployeeLeaveBalances balance WITH (UPDLOCK, HOLDLOCK)
              ON balance.TenantId = @TenantId
             AND balance.CompanyId = @CompanyId
             AND balance.EmployeeId = @EmployeeId
             AND balance.LeaveTypeId = @LeaveTypeId
             AND balance.LeaveYear = allocation.LeaveYear
            WHERE balance.EmployeeLeaveBalanceId IS NULL
               OR (balance.OpeningDays + balance.AccruedDays + balance.CarriedForwardDays
                    + balance.AdjustmentDays - balance.UsedDays - balance.ReservedDays) < allocation.Days
        )
            THROW 51288, 'One or more annual leave balances are missing or have insufficient available days.', 1;

        DECLARE @Status varchar(20) = CASE WHEN @RequiresApproval = 1 THEN 'Pending' ELSE 'Approved' END;
        DECLARE @LeaveRequestId uniqueidentifier = NEWSEQUENTIALID();

        INSERT INTO hrms.LeaveRequests
        (
            LeaveRequestId, TenantId, CompanyId, EmployeeId, LeaveTypeId,
            FromDate, ToDate, IsHalfDay, HalfDayPeriod, DaysRequested, Reason,
            Status, ReviewedAtUtc, CreatedByUserId
        )
        VALUES
        (
            @LeaveRequestId, @TenantId, @CompanyId, @EmployeeId, @LeaveTypeId,
            @FromDate, @ToDate, @IsHalfDay, @HalfDayPeriod, @DaysRequested, @Reason,
            @Status, CASE WHEN @RequiresApproval = 0 THEN SYSUTCDATETIME() END, @ActorUserId
        );

        INSERT INTO hrms.LeaveRequestApprovals
            (TenantId, CompanyId, LeaveRequestId, [Action], ActorEmployeeId, Comments)
        VALUES
            (@TenantId, @CompanyId, @LeaveRequestId, 'Submitted', @EmployeeId, NULL);

        IF @RequiresApproval = 0
        BEGIN
            INSERT INTO hrms.LeaveRequestApprovals
                (TenantId, CompanyId, LeaveRequestId, [Action], ActorEmployeeId, Comments)
            VALUES
                (@TenantId, @CompanyId, @LeaveRequestId, 'Approved', @EmployeeId,
                 N'Automatically approved because the leave type does not require approval.');
        END;

        UPDATE balance
        SET ReservedDays = ReservedDays + allocation.Days,
            UpdatedAtUtc = SYSUTCDATETIME(),
            UpdatedByUserId = @ActorUserId
        FROM hrms.EmployeeLeaveBalances balance
        INNER JOIN @Allocations allocation ON allocation.LeaveYear = balance.LeaveYear
        WHERE @RequiresApproval = 1
          AND balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId
          AND balance.EmployeeId = @EmployeeId AND balance.LeaveTypeId = @LeaveTypeId;

        UPDATE balance
        SET UsedDays = UsedDays + allocation.Days,
            UpdatedAtUtc = SYSUTCDATETIME(),
            UpdatedByUserId = @ActorUserId
        FROM hrms.EmployeeLeaveBalances balance
        INNER JOIN @Allocations allocation ON allocation.LeaveYear = balance.LeaveYear
        WHERE @RequiresApproval = 0
          AND balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId
          AND balance.EmployeeId = @EmployeeId AND balance.LeaveTypeId = @LeaveTypeId;

        INSERT INTO hrms.EmployeeLeaveBalanceTransactions
            (TenantId, CompanyId, EmployeeLeaveBalanceId, LeaveRequestId,
             TransactionType, DaysDelta, Notes, CreatedByUserId)
        SELECT @TenantId, @CompanyId, balance.EmployeeLeaveBalanceId, @LeaveRequestId,
               CASE WHEN @RequiresApproval = 1 THEN 'Reservation' ELSE 'Usage' END,
               allocation.Days,
               CASE WHEN @RequiresApproval = 1 THEN N'Leave request reservation'
                    ELSE N'Auto-approved leave request usage' END,
               @ActorUserId
        FROM hrms.EmployeeLeaveBalances balance
        INNER JOIN @Allocations allocation ON allocation.LeaveYear = balance.LeaveYear
        WHERE balance.TenantId = @TenantId AND balance.CompanyId = @CompanyId
          AND balance.EmployeeId = @EmployeeId AND balance.LeaveTypeId = @LeaveTypeId;

        COMMIT TRANSACTION;

        SELECT request.LeaveRequestId, request.CompanyId, request.EmployeeId,
               request.LeaveTypeId, leaveType.LeaveTypeCode, leaveType.LeaveTypeName,
               request.FromDate, request.ToDate, request.IsHalfDay, request.HalfDayPeriod,
               request.DaysRequested, request.Reason, request.Status,
               request.SubmittedAtUtc, request.RowVersion
        FROM hrms.LeaveRequests request
        INNER JOIN hrms.LeaveTypes leaveType
            ON leaveType.TenantId = request.TenantId
           AND leaveType.CompanyId = request.CompanyId
           AND leaveType.LeaveTypeId = request.LeaveTypeId
        WHERE request.TenantId = @TenantId AND request.CompanyId = @CompanyId
          AND request.LeaveRequestId = @LeaveRequestId;

        SELECT LeaveYear, Days FROM @Allocations ORDER BY LeaveYear;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
