/*
    Migration: 017_LeaveManagementFoundation
    Purpose:
      - Configure company default and office-specific working weeks.
      - Map HRMS employees explicitly to authenticated users.
      - Add leave balances, requests, approval history and balance ledger.
*/
SET XACT_ABORT ON;

CREATE TABLE [hrms].[WorkingWeekDays]
(
    [WorkingWeekDayId] uniqueidentifier NOT NULL CONSTRAINT [DF_WorkingWeekDays_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [ScopeType] varchar(10) NOT NULL,
    [OfficeId] uniqueidentifier NULL,
    [DayOfWeek] tinyint NOT NULL, -- ISO-8601: Monday=1 ... Sunday=7
    [IsWorkingDay] bit NOT NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_WorkingWeekDays_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_WorkingWeekDays] PRIMARY KEY CLUSTERED ([WorkingWeekDayId]),
    CONSTRAINT [CK_WorkingWeekDays_Scope] CHECK
        (([ScopeType] = 'Company' AND [OfficeId] IS NULL) OR ([ScopeType] = 'Office' AND [OfficeId] IS NOT NULL)),
    CONSTRAINT [CK_WorkingWeekDays_Day] CHECK ([DayOfWeek] BETWEEN 1 AND 7),
    CONSTRAINT [FK_WorkingWeekDays_Company] FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [FK_WorkingWeekDays_Office] FOREIGN KEY ([TenantId], [CompanyId], [OfficeId])
        REFERENCES [hrms].[Offices] ([TenantId], [CompanyId], [OfficeId])
);
GO
CREATE UNIQUE INDEX [UX_WorkingWeekDays_Scope_Day]
    ON [hrms].[WorkingWeekDays] ([TenantId], [CompanyId], [ScopeType], [OfficeId], [DayOfWeek]);
GO

CREATE TABLE [hrms].[EmployeeUserMappings]
(
    [EmployeeUserMappingId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeUserMappings_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_EmployeeUserMappings_IsActive] DEFAULT (1),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeUserMappings_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeUserMappings] PRIMARY KEY CLUSTERED ([EmployeeUserMappingId]),
    CONSTRAINT [FK_EmployeeUserMappings_Company] FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [FK_EmployeeUserMappings_Employee] FOREIGN KEY ([CompanyId], [EmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_EmployeeUserMappings_User] FOREIGN KEY ([UserId])
        REFERENCES [security].[Users] ([UserId]),
    CONSTRAINT [FK_EmployeeUserMappings_TenantMembership] FOREIGN KEY ([TenantId], [UserId])
        REFERENCES [security].[UserTenantMemberships] ([TenantId], [UserId])
);
GO
CREATE UNIQUE INDEX [UX_EmployeeUserMappings_ActiveEmployee]
    ON [hrms].[EmployeeUserMappings] ([TenantId], [CompanyId], [EmployeeId]) WHERE [IsActive] = 1;
GO
CREATE UNIQUE INDEX [UX_EmployeeUserMappings_ActiveUser]
    ON [hrms].[EmployeeUserMappings] ([TenantId], [UserId]) WHERE [IsActive] = 1;
GO

CREATE TABLE [hrms].[EmployeeLeaveBalances]
(
    [EmployeeLeaveBalanceId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [LeaveYear] smallint NOT NULL,
    [OpeningDays] decimal(7,2) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_Opening] DEFAULT (0),
    [AccruedDays] decimal(7,2) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_Accrued] DEFAULT (0),
    [CarriedForwardDays] decimal(7,2) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_CarryForward] DEFAULT (0),
    [UsedDays] decimal(7,2) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_Used] DEFAULT (0),
    [ReservedDays] decimal(7,2) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_Reserved] DEFAULT (0),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalances_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeLeaveBalances] PRIMARY KEY CLUSTERED ([EmployeeLeaveBalanceId]),
    CONSTRAINT [UQ_EmployeeLeaveBalances_Tenant_Company_Id] UNIQUE ([TenantId], [CompanyId], [EmployeeLeaveBalanceId]),
    CONSTRAINT [FK_EmployeeLeaveBalances_Company] FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [FK_EmployeeLeaveBalances_Employee] FOREIGN KEY ([CompanyId], [EmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_EmployeeLeaveBalances_LeaveType] FOREIGN KEY ([TenantId], [CompanyId], [LeaveTypeId])
        REFERENCES [hrms].[LeaveTypes] ([TenantId], [CompanyId], [LeaveTypeId]),
    CONSTRAINT [CK_EmployeeLeaveBalances_Year] CHECK ([LeaveYear] BETWEEN 2000 AND 2100),
    CONSTRAINT [CK_EmployeeLeaveBalances_NonNegative] CHECK
        ([OpeningDays] >= 0 AND [AccruedDays] >= 0 AND [CarriedForwardDays] >= 0 AND [UsedDays] >= 0 AND [ReservedDays] >= 0),
    CONSTRAINT [CK_EmployeeLeaveBalances_NotOverdrawn] CHECK
        ([UsedDays] + [ReservedDays] <= [OpeningDays] + [AccruedDays] + [CarriedForwardDays])
);
GO
CREATE UNIQUE INDEX [UX_EmployeeLeaveBalances_Employee_Type_Year]
    ON [hrms].[EmployeeLeaveBalances] ([TenantId], [CompanyId], [EmployeeId], [LeaveTypeId], [LeaveYear]);
GO

CREATE TABLE [hrms].[LeaveRequests]
(
    [LeaveRequestId] uniqueidentifier NOT NULL CONSTRAINT [DF_LeaveRequests_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [IsHalfDay] bit NOT NULL CONSTRAINT [DF_LeaveRequests_IsHalfDay] DEFAULT (0),
    [HalfDayPeriod] varchar(2) NULL,
    [DaysRequested] decimal(7,2) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [Status] varchar(20) NOT NULL CONSTRAINT [DF_LeaveRequests_Status] DEFAULT ('Pending'),
    [SubmittedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_LeaveRequests_SubmittedAtUtc] DEFAULT SYSUTCDATETIME(),
    [ReviewedByEmployeeId] uniqueidentifier NULL,
    [ReviewedAtUtc] datetime2(7) NULL,
    [ReviewComments] nvarchar(1000) NULL,
    [CancelledAtUtc] datetime2(7) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_LeaveRequests_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_LeaveRequests] PRIMARY KEY CLUSTERED ([LeaveRequestId]),
    CONSTRAINT [UQ_LeaveRequests_Tenant_Company_Id] UNIQUE ([TenantId], [CompanyId], [LeaveRequestId]),
    CONSTRAINT [FK_LeaveRequests_Company] FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [FK_LeaveRequests_Employee] FOREIGN KEY ([CompanyId], [EmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_LeaveRequests_LeaveType] FOREIGN KEY ([TenantId], [CompanyId], [LeaveTypeId])
        REFERENCES [hrms].[LeaveTypes] ([TenantId], [CompanyId], [LeaveTypeId]),
    CONSTRAINT [FK_LeaveRequests_Reviewer] FOREIGN KEY ([CompanyId], [ReviewedByEmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_LeaveRequests_Dates] CHECK ([ToDate] >= [FromDate]),
    CONSTRAINT [CK_LeaveRequests_Days] CHECK ([DaysRequested] > 0),
    CONSTRAINT [CK_LeaveRequests_HalfDay] CHECK
        (([IsHalfDay] = 0 AND [HalfDayPeriod] IS NULL) OR
         ([IsHalfDay] = 1 AND [FromDate] = [ToDate] AND [HalfDayPeriod] IS NOT NULL AND [HalfDayPeriod] IN ('AM','PM'))),
    CONSTRAINT [CK_LeaveRequests_Status] CHECK ([Status] IN ('Pending','Approved','Rejected','Cancelled'))
);
GO
CREATE INDEX [IX_LeaveRequests_Employee_Dates]
    ON [hrms].[LeaveRequests] ([TenantId], [CompanyId], [EmployeeId], [FromDate], [ToDate], [Status]);
GO
CREATE INDEX [IX_LeaveRequests_ApprovalQueue]
    ON [hrms].[LeaveRequests] ([TenantId], [CompanyId], [Status], [SubmittedAtUtc])
    INCLUDE ([EmployeeId], [LeaveTypeId], [FromDate], [ToDate], [DaysRequested]);
GO

CREATE TABLE [hrms].[LeaveRequestApprovals]
(
    [LeaveRequestApprovalId] uniqueidentifier NOT NULL CONSTRAINT [DF_LeaveRequestApprovals_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [LeaveRequestId] uniqueidentifier NOT NULL,
    [Action] varchar(20) NOT NULL,
    [ActorEmployeeId] uniqueidentifier NOT NULL,
    [Comments] nvarchar(1000) NULL,
    [ActionAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_LeaveRequestApprovals_ActionAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_LeaveRequestApprovals] PRIMARY KEY CLUSTERED ([LeaveRequestApprovalId]),
    CONSTRAINT [FK_LeaveRequestApprovals_Request] FOREIGN KEY ([TenantId], [CompanyId], [LeaveRequestId])
        REFERENCES [hrms].[LeaveRequests] ([TenantId], [CompanyId], [LeaveRequestId]),
    CONSTRAINT [FK_LeaveRequestApprovals_Actor] FOREIGN KEY ([CompanyId], [ActorEmployeeId])
        REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_LeaveRequestApprovals_Action] CHECK ([Action] IN ('Submitted','Approved','Rejected','Cancelled'))
);
GO

CREATE TABLE [hrms].[EmployeeLeaveBalanceTransactions]
(
    [EmployeeLeaveBalanceTransactionId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeLeaveBalanceTransactions_Id] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeLeaveBalanceId] uniqueidentifier NOT NULL,
    [LeaveRequestId] uniqueidentifier NULL,
    [TransactionType] varchar(20) NOT NULL,
    [DaysDelta] decimal(7,2) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeLeaveBalanceTransactions_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_EmployeeLeaveBalanceTransactions] PRIMARY KEY CLUSTERED ([EmployeeLeaveBalanceTransactionId]),
    CONSTRAINT [FK_EmployeeLeaveBalanceTransactions_Balance] FOREIGN KEY ([TenantId], [CompanyId], [EmployeeLeaveBalanceId])
        REFERENCES [hrms].[EmployeeLeaveBalances] ([TenantId], [CompanyId], [EmployeeLeaveBalanceId]),
    CONSTRAINT [FK_EmployeeLeaveBalanceTransactions_Request] FOREIGN KEY ([TenantId], [CompanyId], [LeaveRequestId])
        REFERENCES [hrms].[LeaveRequests] ([TenantId], [CompanyId], [LeaveRequestId]),
    CONSTRAINT [CK_EmployeeLeaveBalanceTransactions_Type] CHECK
        ([TransactionType] IN ('Opening','Accrual','CarryForward','Reservation','ReservationRelease','Usage','Restoration','Adjustment')),
    CONSTRAINT [CK_EmployeeLeaveBalanceTransactions_Delta] CHECK ([DaysDelta] <> 0)
);
GO
CREATE INDEX [IX_EmployeeLeaveBalanceTransactions_Balance_Date]
    ON [hrms].[EmployeeLeaveBalanceTransactions] ([TenantId], [CompanyId], [EmployeeLeaveBalanceId], [CreatedAtUtc] DESC);
GO
