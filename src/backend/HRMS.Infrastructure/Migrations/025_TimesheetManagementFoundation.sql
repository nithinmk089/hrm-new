/*
    Migration: 025_TimesheetManagementFoundation
    Purpose:
      - Create weekly employee timesheets and daily work entries.
      - Preserve submission and manager decision history.
      - Enforce tenant/company/employee relationships and basic data integrity.
*/
SET XACT_ABORT ON;

CREATE TABLE [hrms].[Timesheets]
(
    [TimesheetId] uniqueidentifier NOT NULL CONSTRAINT [DF_Timesheets_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [WeekStartDate] date NOT NULL,
    [WeekEndDate] date NOT NULL,
    [Status] varchar(20) NOT NULL CONSTRAINT [DF_Timesheets_Status] DEFAULT ('Draft'),
    [SubmittedAtUtc] datetime2(7) NULL,
    [ReviewedByEmployeeId] uniqueidentifier NULL,
    [ReviewedAtUtc] datetime2(7) NULL,
    [ReviewComments] nvarchar(1000) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_Timesheets_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Timesheets] PRIMARY KEY CLUSTERED ([TimesheetId]),
    CONSTRAINT [UQ_Timesheets_Tenant_Company_Id] UNIQUE ([TenantId], [CompanyId], [TimesheetId]),
    CONSTRAINT [FK_Timesheets_Company] FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [FK_Timesheets_Employee] FOREIGN KEY ([CompanyId], [EmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_Timesheets_Reviewer] FOREIGN KEY ([CompanyId], [ReviewedByEmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_Timesheets_WeekRange] CHECK
        ([WeekEndDate] = DATEADD(day, 6, [WeekStartDate])
         AND DATEDIFF(day, CONVERT(date, '19000101'), [WeekStartDate]) % 7 = 0),
    CONSTRAINT [CK_Timesheets_Status] CHECK ([Status] IN ('Draft', 'Submitted', 'Approved', 'Rejected')),
    CONSTRAINT [CK_Timesheets_SubmissionState] CHECK
        (([Status] = 'Draft' AND [SubmittedAtUtc] IS NULL)
         OR ([Status] <> 'Draft' AND [SubmittedAtUtc] IS NOT NULL)),
    CONSTRAINT [CK_Timesheets_ReviewState] CHECK
        (([Status] IN ('Draft', 'Submitted') AND [ReviewedByEmployeeId] IS NULL AND [ReviewedAtUtc] IS NULL)
         OR ([Status] IN ('Approved', 'Rejected') AND [ReviewedByEmployeeId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL))
);
GO

CREATE UNIQUE INDEX [UX_Timesheets_Employee_Week_Active]
    ON [hrms].[Timesheets] ([TenantId], [CompanyId], [EmployeeId], [WeekStartDate])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE INDEX [IX_Timesheets_ApprovalQueue]
    ON [hrms].[Timesheets] ([TenantId], [CompanyId], [Status], [SubmittedAtUtc])
    INCLUDE ([EmployeeId], [WeekStartDate], [WeekEndDate])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE INDEX [IX_Timesheets_Employee_History]
    ON [hrms].[Timesheets] ([TenantId], [CompanyId], [EmployeeId], [WeekStartDate] DESC)
    INCLUDE ([Status], [SubmittedAtUtc])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[TimesheetEntries]
(
    [TimesheetEntryId] uniqueidentifier NOT NULL CONSTRAINT [DF_TimesheetEntries_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [TimesheetId] uniqueidentifier NOT NULL,
    [WorkDate] date NOT NULL,
    [WorkCategory] varchar(30) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Hours] decimal(5,2) NOT NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_TimesheetEntries_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_TimesheetEntries] PRIMARY KEY CLUSTERED ([TimesheetEntryId]),
    CONSTRAINT [FK_TimesheetEntries_Timesheet] FOREIGN KEY ([TenantId], [CompanyId], [TimesheetId])
        REFERENCES [hrms].[Timesheets] ([TenantId], [CompanyId], [TimesheetId]),
    CONSTRAINT [CK_TimesheetEntries_Category] CHECK
        ([WorkCategory] IN ('ClientWork', 'Internal', 'Meeting', 'Training', 'Administration', 'Other')),
    CONSTRAINT [CK_TimesheetEntries_Description] CHECK (LEN(LTRIM(RTRIM([Description]))) > 0),
    CONSTRAINT [CK_TimesheetEntries_Hours] CHECK
        ([Hours] > 0 AND [Hours] <= 24 AND [Hours] * 4 = FLOOR([Hours] * 4))
);
GO

CREATE INDEX [IX_TimesheetEntries_Timesheet_Date]
    ON [hrms].[TimesheetEntries] ([TenantId], [CompanyId], [TimesheetId], [WorkDate])
    INCLUDE ([WorkCategory], [Hours])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[TimesheetHistory]
(
    [TimesheetHistoryId] uniqueidentifier NOT NULL CONSTRAINT [DF_TimesheetHistory_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [TimesheetId] uniqueidentifier NOT NULL,
    [Action] varchar(20) NOT NULL,
    [ActorEmployeeId] uniqueidentifier NOT NULL,
    [Comments] nvarchar(1000) NULL,
    [ActionAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_TimesheetHistory_ActionAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_TimesheetHistory] PRIMARY KEY CLUSTERED ([TimesheetHistoryId]),
    CONSTRAINT [FK_TimesheetHistory_Timesheet] FOREIGN KEY ([TenantId], [CompanyId], [TimesheetId])
        REFERENCES [hrms].[Timesheets] ([TenantId], [CompanyId], [TimesheetId]),
    CONSTRAINT [FK_TimesheetHistory_Actor] FOREIGN KEY ([CompanyId], [ActorEmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_TimesheetHistory_Action] CHECK
        ([Action] IN ('Created', 'Submitted', 'Approved', 'Rejected', 'Resubmitted'))
);
GO

CREATE INDEX [IX_TimesheetHistory_Timesheet_ActionAt]
    ON [hrms].[TimesheetHistory] ([TenantId], [CompanyId], [TimesheetId], [ActionAtUtc]);
GO
