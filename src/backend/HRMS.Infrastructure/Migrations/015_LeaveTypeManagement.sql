/*
    Migration: 015_LeaveTypeManagement
    Purpose:
      - Create tenant/company-scoped leave-type configuration.
      - Enforce unique leave-type codes per company.
      - Add stored procedures with optimistic concurrency.
*/
CREATE TABLE [hrms].[LeaveTypes]
(
    [LeaveTypeId] uniqueidentifier NOT NULL
        CONSTRAINT [DF_LeaveTypes_LeaveTypeId] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [LeaveTypeCode] nvarchar(30) NOT NULL,
    [LeaveTypeName] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsPaid] bit NOT NULL CONSTRAINT [DF_LeaveTypes_IsPaid] DEFAULT (1),
    [RequiresApproval] bit NOT NULL CONSTRAINT [DF_LeaveTypes_RequiresApproval] DEFAULT (1),
    [AnnualEntitlementDays] decimal(6,2) NULL,
    [AllowHalfDay] bit NOT NULL CONSTRAINT [DF_LeaveTypes_AllowHalfDay] DEFAULT (0),
    [AllowCarryForward] bit NOT NULL CONSTRAINT [DF_LeaveTypes_AllowCarryForward] DEFAULT (0),
    [MaxCarryForwardDays] decimal(6,2) NOT NULL CONSTRAINT [DF_LeaveTypes_MaxCarryForwardDays] DEFAULT (0),
    [IsActive] bit NOT NULL CONSTRAINT [DF_LeaveTypes_IsActive] DEFAULT (1),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_LeaveTypes_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_LeaveTypes] PRIMARY KEY CLUSTERED ([LeaveTypeId]),
    CONSTRAINT [UQ_LeaveTypes_Tenant_Company_LeaveType]
        UNIQUE ([TenantId], [CompanyId], [LeaveTypeId]),
    CONSTRAINT [FK_LeaveTypes_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [CK_LeaveTypes_Code_NotBlank]
        CHECK (LEN(LTRIM(RTRIM([LeaveTypeCode]))) > 0),
    CONSTRAINT [CK_LeaveTypes_Name_NotBlank]
        CHECK (LEN(LTRIM(RTRIM([LeaveTypeName]))) > 0),
    CONSTRAINT [CK_LeaveTypes_AnnualEntitlement]
        CHECK ([AnnualEntitlementDays] IS NULL OR ([AnnualEntitlementDays] >= 0 AND [AnnualEntitlementDays] <= 366)),
    CONSTRAINT [CK_LeaveTypes_MaxCarryForward]
        CHECK ([MaxCarryForwardDays] >= 0 AND [MaxCarryForwardDays] <= 366),
    CONSTRAINT [CK_LeaveTypes_CarryForward]
        CHECK
        (
            ([AllowCarryForward] = 0 AND [MaxCarryForwardDays] = 0)
            OR ([AllowCarryForward] = 1 AND [MaxCarryForwardDays] > 0)
        )
);
GO

CREATE UNIQUE INDEX [UX_LeaveTypes_Company_Code]
    ON [hrms].[LeaveTypes] ([TenantId], [CompanyId], [LeaveTypeCode])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE INDEX [IX_LeaveTypes_Company_Active]
    ON [hrms].[LeaveTypes] ([TenantId], [CompanyId], [IsActive])
    INCLUDE ([LeaveTypeCode], [LeaveTypeName], [IsPaid], [RequiresApproval])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveTypes_GetAll]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LeaveTypeId, CompanyId, LeaveTypeCode, LeaveTypeName, Description,
           IsPaid, RequiresApproval, AnnualEntitlementDays, AllowHalfDay,
           AllowCarryForward, MaxCarryForwardDays, IsActive,
           CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.LeaveTypes
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND DeletedAtUtc IS NULL
    ORDER BY LeaveTypeName, LeaveTypeCode;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveTypes_GetById]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @LeaveTypeId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LeaveTypeId, CompanyId, LeaveTypeCode, LeaveTypeName, Description,
           IsPaid, RequiresApproval, AnnualEntitlementDays, AllowHalfDay,
           AllowCarryForward, MaxCarryForwardDays, IsActive,
           CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.LeaveTypes
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND LeaveTypeId = @LeaveTypeId AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveTypes_GetByCode]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @LeaveTypeCode nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LeaveTypeId, CompanyId, LeaveTypeCode, LeaveTypeName, Description,
           IsPaid, RequiresApproval, AnnualEntitlementDays, AllowHalfDay,
           AllowCarryForward, MaxCarryForwardDays, IsActive,
           CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.LeaveTypes
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND LeaveTypeCode = @LeaveTypeCode AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveTypes_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @LeaveTypeCode nvarchar(30),
    @LeaveTypeName nvarchar(100),
    @Description nvarchar(500) = NULL,
    @IsPaid bit,
    @RequiresApproval bit,
    @AnnualEntitlementDays decimal(6,2) = NULL,
    @AllowHalfDay bit,
    @AllowCarryForward bit,
    @MaxCarryForwardDays decimal(6,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.Companies
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51241, 'The company does not exist, is inactive, or is outside the authenticated tenant.', 1;

    INSERT INTO hrms.LeaveTypes
    (
        TenantId, CompanyId, LeaveTypeCode, LeaveTypeName, Description,
        IsPaid, RequiresApproval, AnnualEntitlementDays, AllowHalfDay,
        AllowCarryForward, MaxCarryForwardDays
    )
    OUTPUT INSERTED.LeaveTypeId, INSERTED.CompanyId, INSERTED.LeaveTypeCode,
           INSERTED.LeaveTypeName, INSERTED.Description, INSERTED.IsPaid,
           INSERTED.RequiresApproval, INSERTED.AnnualEntitlementDays,
           INSERTED.AllowHalfDay, INSERTED.AllowCarryForward,
           INSERTED.MaxCarryForwardDays, INSERTED.IsActive,
           INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    VALUES
    (
        @TenantId, @CompanyId, @LeaveTypeCode, @LeaveTypeName, @Description,
        @IsPaid, @RequiresApproval, @AnnualEntitlementDays, @AllowHalfDay,
        @AllowCarryForward, @MaxCarryForwardDays
    );
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveTypes_Update]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @LeaveTypeId uniqueidentifier,
    @LeaveTypeName nvarchar(100),
    @Description nvarchar(500) = NULL,
    @IsPaid bit,
    @RequiresApproval bit,
    @AnnualEntitlementDays decimal(6,2) = NULL,
    @AllowHalfDay bit,
    @AllowCarryForward bit,
    @MaxCarryForwardDays decimal(6,2),
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.LeaveTypes
    SET LeaveTypeName = @LeaveTypeName,
        Description = @Description,
        IsPaid = @IsPaid,
        RequiresApproval = @RequiresApproval,
        AnnualEntitlementDays = @AnnualEntitlementDays,
        AllowHalfDay = @AllowHalfDay,
        AllowCarryForward = @AllowCarryForward,
        MaxCarryForwardDays = @MaxCarryForwardDays,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.LeaveTypeId, INSERTED.CompanyId, INSERTED.LeaveTypeCode,
           INSERTED.LeaveTypeName, INSERTED.Description, INSERTED.IsPaid,
           INSERTED.RequiresApproval, INSERTED.AnnualEntitlementDays,
           INSERTED.AllowHalfDay, INSERTED.AllowCarryForward,
           INSERTED.MaxCarryForwardDays, INSERTED.IsActive,
           INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND LeaveTypeId = @LeaveTypeId AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_LeaveTypes_Deactivate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @LeaveTypeId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.LeaveTypes
    SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND LeaveTypeId = @LeaveTypeId AND RowVersion = @RowVersion
      AND IsActive = 1 AND DeletedAtUtc IS NULL;

    SELECT @@ROWCOUNT;
END;
GO
