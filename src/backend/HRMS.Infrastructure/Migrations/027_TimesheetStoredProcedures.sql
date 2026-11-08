/*
    Migration: 027_TimesheetStoredProcedures
    Purpose:
      - Implement employee draft creation, entry replacement and submission.
      - Implement employee history and direct-manager approval workflows.
      - Enforce tenant/company scope, ownership, rowversion and hour limits.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_GetMine]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @WeekStartDate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EmployeeId uniqueidentifier;
    SELECT @EmployeeId = mapping.EmployeeId
    FROM hrms.EmployeeUserMappings mapping
    INNER JOIN security.UserTenantMemberships membership
        ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
    INNER JOIN hrms.Employees employee
        ON employee.TenantId = mapping.TenantId AND employee.CompanyId = mapping.CompanyId
       AND employee.EmployeeId = mapping.EmployeeId
    WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
      AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
      AND membership.MembershipStatus = 'Active'
      AND employee.IsActive = 1 AND employee.DeletedAtUtc IS NULL;

    IF @EmployeeId IS NULL
        THROW 51400, 'The authenticated user is not mapped to an active employee in this company.', 1;

    SELECT timesheet.TimesheetId, timesheet.WeekStartDate, timesheet.WeekEndDate,
           timesheet.Status, timesheet.SubmittedAtUtc, timesheet.ReviewedByEmployeeId,
           timesheet.ReviewedAtUtc, timesheet.ReviewComments, timesheet.RowVersion,
           COALESCE(SUM(entry.Hours), 0) AS TotalHours
    FROM hrms.Timesheets timesheet
    LEFT JOIN hrms.TimesheetEntries entry
        ON entry.TenantId = timesheet.TenantId AND entry.CompanyId = timesheet.CompanyId
       AND entry.TimesheetId = timesheet.TimesheetId AND entry.DeletedAtUtc IS NULL
    WHERE timesheet.TenantId = @TenantId AND timesheet.CompanyId = @CompanyId
      AND timesheet.EmployeeId = @EmployeeId
      AND timesheet.WeekStartDate = @WeekStartDate
      AND timesheet.DeletedAtUtc IS NULL
    GROUP BY timesheet.TimesheetId, timesheet.WeekStartDate, timesheet.WeekEndDate,
             timesheet.Status, timesheet.SubmittedAtUtc, timesheet.ReviewedByEmployeeId,
             timesheet.ReviewedAtUtc, timesheet.ReviewComments, timesheet.RowVersion;

    SELECT entry.TimesheetEntryId, entry.WorkDate, entry.WorkCategory,
           entry.Description, entry.Hours, entry.RowVersion
    FROM hrms.TimesheetEntries entry
    INNER JOIN hrms.Timesheets timesheet
        ON timesheet.TenantId = entry.TenantId AND timesheet.CompanyId = entry.CompanyId
       AND timesheet.TimesheetId = entry.TimesheetId
    WHERE timesheet.TenantId = @TenantId AND timesheet.CompanyId = @CompanyId
      AND timesheet.EmployeeId = @EmployeeId
      AND timesheet.WeekStartDate = @WeekStartDate
      AND timesheet.DeletedAtUtc IS NULL AND entry.DeletedAtUtc IS NULL
    ORDER BY entry.WorkDate, entry.TimesheetEntryId;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_GetMineHistory]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EmployeeId uniqueidentifier;
    SELECT @EmployeeId = mapping.EmployeeId
    FROM hrms.EmployeeUserMappings mapping
    INNER JOIN security.UserTenantMemberships membership
        ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
    WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
      AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
      AND membership.MembershipStatus = 'Active';

    IF @EmployeeId IS NULL
        THROW 51400, 'The authenticated user is not mapped to an active employee in this company.', 1;

    SELECT TOP (100) timesheet.TimesheetId, timesheet.WeekStartDate, timesheet.WeekEndDate,
           timesheet.Status, timesheet.SubmittedAtUtc, timesheet.ReviewedAtUtc,
           timesheet.ReviewComments, timesheet.RowVersion,
           COALESCE(SUM(entry.Hours), 0) AS TotalHours
    FROM hrms.Timesheets timesheet
    LEFT JOIN hrms.TimesheetEntries entry
        ON entry.TenantId = timesheet.TenantId AND entry.CompanyId = timesheet.CompanyId
       AND entry.TimesheetId = timesheet.TimesheetId AND entry.DeletedAtUtc IS NULL
    WHERE timesheet.TenantId = @TenantId AND timesheet.CompanyId = @CompanyId
      AND timesheet.EmployeeId = @EmployeeId AND timesheet.DeletedAtUtc IS NULL
    GROUP BY timesheet.TimesheetId, timesheet.WeekStartDate, timesheet.WeekEndDate,
             timesheet.Status, timesheet.SubmittedAtUtc, timesheet.ReviewedAtUtc,
             timesheet.ReviewComments, timesheet.RowVersion
    ORDER BY timesheet.WeekStartDate DESC;

    SELECT history.TimesheetId, history.[Action], history.ActorEmployeeId,
           history.Comments, history.ActionAtUtc
    FROM hrms.TimesheetHistory history
    INNER JOIN hrms.Timesheets timesheet
        ON timesheet.TenantId = history.TenantId AND timesheet.CompanyId = history.CompanyId
       AND timesheet.TimesheetId = history.TimesheetId
    WHERE history.TenantId = @TenantId AND history.CompanyId = @CompanyId
      AND timesheet.EmployeeId = @EmployeeId AND timesheet.DeletedAtUtc IS NULL
    ORDER BY history.ActionAtUtc DESC, history.TimesheetHistoryId DESC;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @WeekStartDate date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DATEDIFF(day, CONVERT(date, '19000101'), @WeekStartDate) % 7 <> 0
        THROW 51401, 'The timesheet week must start on a Monday.', 1;

    IF @WeekStartDate > CONVERT(date, SYSUTCDATETIME())
        THROW 51402, 'A timesheet cannot be created for a future week.', 1;

    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @TimesheetId uniqueidentifier = NEWID();
    DECLARE @Now datetime2(7) = SYSUTCDATETIME();

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1 FROM hrms.Companies
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND IsActive = 1 AND DeletedAtUtc IS NULL
        )
            THROW 51403, 'The company does not exist or is inactive in the authenticated tenant.', 1;

        SELECT @EmployeeId = mapping.EmployeeId
        FROM hrms.EmployeeUserMappings mapping WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
        INNER JOIN hrms.Employees employee
            ON employee.TenantId = mapping.TenantId AND employee.CompanyId = mapping.CompanyId
           AND employee.EmployeeId = mapping.EmployeeId
        WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
          AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
          AND membership.MembershipStatus = 'Active'
          AND employee.IsActive = 1 AND employee.DeletedAtUtc IS NULL;

        IF @EmployeeId IS NULL
            THROW 51400, 'The authenticated user is not mapped to an active employee in this company.', 1;

        IF EXISTS
        (
            SELECT 1 FROM hrms.Timesheets WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND EmployeeId = @EmployeeId AND WeekStartDate = @WeekStartDate
              AND DeletedAtUtc IS NULL
        )
            THROW 51404, 'A timesheet already exists for this employee and week.', 1;

        INSERT INTO hrms.Timesheets
            (TimesheetId, TenantId, CompanyId, EmployeeId, WeekStartDate, WeekEndDate,
             Status, CreatedAtUtc, CreatedByUserId)
        VALUES
            (@TimesheetId, @TenantId, @CompanyId, @EmployeeId, @WeekStartDate,
             DATEADD(day, 6, @WeekStartDate), 'Draft', @Now, @ActorUserId);

        INSERT INTO hrms.TimesheetHistory
            (TenantId, CompanyId, TimesheetId, [Action], ActorEmployeeId, ActionAtUtc)
        VALUES
            (@TenantId, @CompanyId, @TimesheetId, 'Created', @EmployeeId, @Now);

        COMMIT TRANSACTION;

        SELECT TimesheetId, WeekStartDate, WeekEndDate, Status, RowVersion
        FROM hrms.Timesheets
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_SaveEntries]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @TimesheetId uniqueidentifier,
    @RowVersion binary(8),
    @EntriesJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISJSON(@EntriesJson) <> 1
        THROW 51405, 'Timesheet entries must be a valid JSON array.', 1;

    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @WeekStartDate date;
    DECLARE @Status varchar(20);
    DECLARE @CurrentRowVersion binary(8);
    DECLARE @Now datetime2(7) = SYSUTCDATETIME();

    DECLARE @Entries TABLE
    (
        WorkDate date NOT NULL,
        WorkCategory varchar(30) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        Hours decimal(5,2) NOT NULL
    );

    INSERT INTO @Entries (WorkDate, WorkCategory, [Description], Hours)
    SELECT WorkDate, WorkCategory, [Description], Hours
    FROM OPENJSON(@EntriesJson)
    WITH
    (
        WorkDate date '$.workDate',
        WorkCategory varchar(30) '$.workCategory',
        [Description] nvarchar(1000) '$.description',
        Hours decimal(5,2) '$.hours'
    );

    IF NOT EXISTS (SELECT 1 FROM @Entries)
        THROW 51406, 'At least one time entry is required.', 1;

    IF (SELECT COUNT(*) FROM @Entries) > 200
        THROW 51407, 'A weekly timesheet cannot contain more than 200 entries.', 1;

    IF EXISTS
    (
        SELECT 1 FROM @Entries
        WHERE WorkDate IS NULL OR WorkCategory NOT IN
            ('ClientWork', 'Internal', 'Meeting', 'Training', 'Administration', 'Other')
           OR NULLIF(LTRIM(RTRIM([Description])), N'') IS NULL
           OR LEN([Description]) > 1000
           OR Hours <= 0 OR Hours > 24 OR Hours * 4 <> FLOOR(Hours * 4)
    )
        THROW 51408, 'One or more time entries contain invalid values.', 1;

    IF EXISTS (SELECT 1 FROM @Entries GROUP BY WorkDate HAVING SUM(Hours) > 24)
        THROW 51409, 'Daily recorded hours cannot exceed 24.', 1;

    IF (SELECT SUM(Hours) FROM @Entries) > 168
        THROW 51410, 'Weekly recorded hours cannot exceed 168.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EmployeeId = mapping.EmployeeId
        FROM hrms.EmployeeUserMappings mapping WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
        WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
          AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
          AND membership.MembershipStatus = 'Active';

        IF @EmployeeId IS NULL
            THROW 51400, 'The authenticated user is not mapped to an active employee in this company.', 1;

        SELECT @WeekStartDate = WeekStartDate, @Status = Status, @CurrentRowVersion = RowVersion
        FROM hrms.Timesheets WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId AND EmployeeId = @EmployeeId
          AND DeletedAtUtc IS NULL;

        IF @WeekStartDate IS NULL
            THROW 51411, 'The timesheet was not found for the authenticated employee.', 1;

        IF @CurrentRowVersion <> @RowVersion
            THROW 51412, 'The timesheet has changed. Refresh it and try again.', 1;

        IF @Status NOT IN ('Draft', 'Rejected')
            THROW 51413, 'Only draft or rejected timesheets can be edited.', 1;

        IF EXISTS (SELECT 1 FROM @Entries WHERE WorkDate < @WeekStartDate OR WorkDate > DATEADD(day, 6, @WeekStartDate))
            THROW 51414, 'Every work date must belong to the timesheet week.', 1;

        UPDATE hrms.TimesheetEntries
        SET DeletedAtUtc = @Now, DeletedByUserId = @ActorUserId,
            UpdatedAtUtc = @Now, UpdatedByUserId = @ActorUserId
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId AND DeletedAtUtc IS NULL;

        INSERT INTO hrms.TimesheetEntries
            (TenantId, CompanyId, TimesheetId, WorkDate, WorkCategory, [Description],
             Hours, CreatedAtUtc, CreatedByUserId)
        SELECT @TenantId, @CompanyId, @TimesheetId, WorkDate, WorkCategory,
               LTRIM(RTRIM([Description])), Hours, @Now, @ActorUserId
        FROM @Entries;

        UPDATE hrms.Timesheets
        SET UpdatedAtUtc = @Now, UpdatedByUserId = @ActorUserId
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId AND RowVersion = @RowVersion;

        IF @@ROWCOUNT <> 1
            THROW 51412, 'The timesheet has changed. Refresh it and try again.', 1;

        COMMIT TRANSACTION;

        SELECT TimesheetId, Status, RowVersion,
               (SELECT SUM(Hours) FROM hrms.TimesheetEntries
                WHERE TenantId = @TenantId AND CompanyId = @CompanyId
                  AND TimesheetId = @TimesheetId AND DeletedAtUtc IS NULL) AS TotalHours
        FROM hrms.Timesheets
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_Submit]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @TimesheetId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @Status varchar(20);
    DECLARE @CurrentRowVersion binary(8);
    DECLARE @Now datetime2(7) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @Now);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EmployeeId = mapping.EmployeeId
        FROM hrms.EmployeeUserMappings mapping WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
        WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
          AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
          AND membership.MembershipStatus = 'Active';

        IF @EmployeeId IS NULL
            THROW 51400, 'The authenticated user is not mapped to an active employee in this company.', 1;

        SELECT @Status = Status, @CurrentRowVersion = RowVersion
        FROM hrms.Timesheets WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId AND EmployeeId = @EmployeeId
          AND DeletedAtUtc IS NULL;

        IF @Status IS NULL
            THROW 51411, 'The timesheet was not found for the authenticated employee.', 1;

        IF @CurrentRowVersion <> @RowVersion
            THROW 51412, 'The timesheet has changed. Refresh it and try again.', 1;

        IF @Status NOT IN ('Draft', 'Rejected')
            THROW 51415, 'Only draft or rejected timesheets can be submitted.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM hrms.TimesheetEntries
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND TimesheetId = @TimesheetId AND DeletedAtUtc IS NULL
        )
            THROW 51406, 'At least one time entry is required before submission.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM hrms.EmployeeReportingAssignments assignment
            INNER JOIN hrms.Employees manager
                ON manager.TenantId = assignment.TenantId AND manager.CompanyId = assignment.CompanyId
               AND manager.EmployeeId = assignment.ManagerEmployeeId
            INNER JOIN hrms.EmployeeUserMappings managerMapping
                ON managerMapping.TenantId = assignment.TenantId AND managerMapping.CompanyId = assignment.CompanyId
               AND managerMapping.EmployeeId = assignment.ManagerEmployeeId AND managerMapping.IsActive = 1
            INNER JOIN security.UserTenantMemberships managerMembership
                ON managerMembership.TenantId = managerMapping.TenantId AND managerMembership.UserId = managerMapping.UserId
            WHERE assignment.TenantId = @TenantId AND assignment.CompanyId = @CompanyId
              AND assignment.EmployeeId = @EmployeeId
              AND assignment.StartDate <= @Today
              AND (assignment.EndDate IS NULL OR assignment.EndDate >= @Today)
              AND assignment.DeletedAtUtc IS NULL
              AND manager.IsActive = 1 AND manager.DeletedAtUtc IS NULL
              AND managerMembership.MembershipStatus = 'Active'
        )
        BEGIN
            UPDATE hrms.Timesheets
            SET Status = 'Submitted', SubmittedAtUtc = @Now,
                ReviewedByEmployeeId = NULL, ReviewedAtUtc = NULL, ReviewComments = NULL,
                UpdatedAtUtc = @Now, UpdatedByUserId = @ActorUserId
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND TimesheetId = @TimesheetId AND RowVersion = @RowVersion;

            IF @@ROWCOUNT <> 1
                THROW 51412, 'The timesheet has changed. Refresh it and try again.', 1;

            INSERT INTO hrms.TimesheetHistory
                (TenantId, CompanyId, TimesheetId, [Action], ActorEmployeeId, ActionAtUtc)
            VALUES
                (@TenantId, @CompanyId, @TimesheetId,
                 CASE WHEN @Status = 'Rejected' THEN 'Resubmitted' ELSE 'Submitted' END,
                 @EmployeeId, @Now);
        END
        ELSE
            THROW 51416, 'An active reporting manager with an active user mapping is required before submission.', 1;

        COMMIT TRANSACTION;

        SELECT TimesheetId, Status, SubmittedAtUtc, RowVersion
        FROM hrms.Timesheets
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_GetPendingApprovals]
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
    WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
      AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
      AND membership.MembershipStatus = 'Active';

    IF @ManagerEmployeeId IS NULL
        THROW 51417, 'The authenticated user is not mapped to an active manager employee.', 1;

    SELECT timesheet.TimesheetId, timesheet.EmployeeId,
           employee.EmployeeCode, employee.FirstName, employee.LastName,
           timesheet.WeekStartDate, timesheet.WeekEndDate, timesheet.SubmittedAtUtc,
           COALESCE(SUM(entry.Hours), 0) AS TotalHours, timesheet.RowVersion
    FROM hrms.Timesheets timesheet
    INNER JOIN hrms.Employees employee
        ON employee.TenantId = timesheet.TenantId AND employee.CompanyId = timesheet.CompanyId
       AND employee.EmployeeId = timesheet.EmployeeId
    INNER JOIN hrms.EmployeeReportingAssignments assignment
        ON assignment.TenantId = timesheet.TenantId AND assignment.CompanyId = timesheet.CompanyId
       AND assignment.EmployeeId = timesheet.EmployeeId
    LEFT JOIN hrms.TimesheetEntries entry
        ON entry.TenantId = timesheet.TenantId AND entry.CompanyId = timesheet.CompanyId
       AND entry.TimesheetId = timesheet.TimesheetId AND entry.DeletedAtUtc IS NULL
    WHERE timesheet.TenantId = @TenantId AND timesheet.CompanyId = @CompanyId
      AND timesheet.Status = 'Submitted' AND timesheet.DeletedAtUtc IS NULL
      AND assignment.ManagerEmployeeId = @ManagerEmployeeId
      AND assignment.StartDate <= CONVERT(date, SYSUTCDATETIME())
      AND (assignment.EndDate IS NULL OR assignment.EndDate >= CONVERT(date, SYSUTCDATETIME()))
      AND assignment.DeletedAtUtc IS NULL
    GROUP BY timesheet.TimesheetId, timesheet.EmployeeId, employee.EmployeeCode,
             employee.FirstName, employee.LastName, timesheet.WeekStartDate,
             timesheet.WeekEndDate, timesheet.SubmittedAtUtc, timesheet.RowVersion
    ORDER BY timesheet.SubmittedAtUtc, timesheet.WeekStartDate;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Timesheets_Decide]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @ActorUserId uniqueidentifier,
    @TimesheetId uniqueidentifier,
    @Decision varchar(10),
    @RowVersion binary(8),
    @Comments nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Decision NOT IN ('Approved', 'Rejected')
        THROW 51418, 'The timesheet decision is invalid.', 1;

    IF @Decision = 'Rejected' AND NULLIF(LTRIM(RTRIM(@Comments)), N'') IS NULL
        THROW 51419, 'A reason is required when rejecting a timesheet.', 1;

    DECLARE @ManagerEmployeeId uniqueidentifier;
    DECLARE @EmployeeId uniqueidentifier;
    DECLARE @Status varchar(20);
    DECLARE @CurrentRowVersion binary(8);
    DECLARE @Now datetime2(7) = SYSUTCDATETIME();
    DECLARE @Today date = CONVERT(date, @Now);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @ManagerEmployeeId = mapping.EmployeeId
        FROM hrms.EmployeeUserMappings mapping WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN security.UserTenantMemberships membership
            ON membership.TenantId = mapping.TenantId AND membership.UserId = mapping.UserId
        WHERE mapping.TenantId = @TenantId AND mapping.CompanyId = @CompanyId
          AND mapping.UserId = @ActorUserId AND mapping.IsActive = 1
          AND membership.MembershipStatus = 'Active';

        IF @ManagerEmployeeId IS NULL
            THROW 51417, 'The authenticated user is not mapped to an active manager employee.', 1;

        SELECT @EmployeeId = EmployeeId, @Status = Status, @CurrentRowVersion = RowVersion
        FROM hrms.Timesheets WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId AND DeletedAtUtc IS NULL;

        IF @EmployeeId IS NULL
            THROW 51420, 'The timesheet was not found in this company.', 1;

        IF @EmployeeId = @ManagerEmployeeId
            THROW 51421, 'A manager cannot approve or reject their own timesheet.', 1;

        IF @Status <> 'Submitted'
            THROW 51422, 'Only submitted timesheets can be approved or rejected.', 1;

        IF @CurrentRowVersion <> @RowVersion
            THROW 51412, 'The timesheet has changed. Refresh it and try again.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM hrms.EmployeeReportingAssignments assignment WITH (UPDLOCK, HOLDLOCK)
            WHERE assignment.TenantId = @TenantId AND assignment.CompanyId = @CompanyId
              AND assignment.EmployeeId = @EmployeeId
              AND assignment.ManagerEmployeeId = @ManagerEmployeeId
              AND assignment.StartDate <= @Today
              AND (assignment.EndDate IS NULL OR assignment.EndDate >= @Today)
              AND assignment.DeletedAtUtc IS NULL
        )
            THROW 51423, 'Only the employee''s currently assigned reporting manager can decide this timesheet.', 1;

        UPDATE hrms.Timesheets
        SET Status = @Decision,
            ReviewedByEmployeeId = @ManagerEmployeeId,
            ReviewedAtUtc = @Now,
            ReviewComments = NULLIF(LTRIM(RTRIM(@Comments)), N''),
            UpdatedAtUtc = @Now,
            UpdatedByUserId = @ActorUserId
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId AND Status = 'Submitted'
          AND RowVersion = @RowVersion;

        IF @@ROWCOUNT <> 1
            THROW 51412, 'The timesheet has changed. Refresh it and try again.', 1;

        INSERT INTO hrms.TimesheetHistory
            (TenantId, CompanyId, TimesheetId, [Action], ActorEmployeeId, Comments, ActionAtUtc)
        VALUES
            (@TenantId, @CompanyId, @TimesheetId, @Decision,
             @ManagerEmployeeId, NULLIF(LTRIM(RTRIM(@Comments)), N''), @Now);

        COMMIT TRANSACTION;

        SELECT TimesheetId, Status, ReviewedByEmployeeId, ReviewedAtUtc, RowVersion
        FROM hrms.Timesheets
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND TimesheetId = @TimesheetId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
