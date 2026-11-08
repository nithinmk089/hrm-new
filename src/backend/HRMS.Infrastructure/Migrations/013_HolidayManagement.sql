/*
    Migration: 013_HolidayManagement
    Purpose:
      - Create tenant/company-scoped company holiday records.
      - Enforce one holiday date per company (including inactive records).
      - Add tenant-scoped stored procedures with RowVersion concurrency.
*/
CREATE TABLE [hrms].[Holidays]
(
    [HolidayId] uniqueidentifier NOT NULL
        CONSTRAINT [DF_Holidays_HolidayId] DEFAULT NEWSEQUENTIALID(),
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [HolidayName] nvarchar(150) NOT NULL,
    [HolidayDate] date NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsOptional] bit NOT NULL CONSTRAINT [DF_Holidays_IsOptional] DEFAULT (0),
    [IsActive] bit NOT NULL CONSTRAINT [DF_Holidays_IsActive] DEFAULT (1),
    [CreatedAtUtc] datetime2(7) NOT NULL
        CONSTRAINT [DF_Holidays_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY CLUSTERED ([HolidayId]),
    CONSTRAINT [UQ_Holidays_Tenant_Company_Holiday]
        UNIQUE ([TenantId], [CompanyId], [HolidayId]),
    CONSTRAINT [FK_Holidays_TenantCompany]
        FOREIGN KEY ([TenantId], [CompanyId])
        REFERENCES [hrms].[Companies] ([TenantId], [CompanyId]),
    CONSTRAINT [CK_Holidays_HolidayName_NotBlank]
        CHECK (LEN(LTRIM(RTRIM([HolidayName]))) > 0)
);
GO

CREATE UNIQUE INDEX [UX_Holidays_Company_Date]
    ON [hrms].[Holidays] ([TenantId], [CompanyId], [HolidayDate])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE INDEX [IX_Holidays_Company_Date_Active]
    ON [hrms].[Holidays] ([TenantId], [CompanyId], [HolidayDate])
    INCLUDE ([HolidayName], [IsOptional], [IsActive])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Holidays_GetAll]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT HolidayId, CompanyId, HolidayName, HolidayDate, Description, IsOptional,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Holidays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND DeletedAtUtc IS NULL
    ORDER BY HolidayDate, HolidayName;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Holidays_GetById]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @HolidayId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT HolidayId, CompanyId, HolidayName, HolidayDate, Description, IsOptional,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Holidays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND HolidayId = @HolidayId AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Holidays_GetByDate]
    @TenantId uniqueidentifier, @CompanyId uniqueidentifier, @HolidayDate date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT HolidayId, CompanyId, HolidayName, HolidayDate, Description, IsOptional,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Holidays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND HolidayDate = @HolidayDate AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Holidays_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @HolidayName nvarchar(150),
    @HolidayDate date,
    @Description nvarchar(500) = NULL,
    @IsOptional bit = 0
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
        THROW 51231, 'The company does not exist, is inactive, or is outside the authenticated tenant.', 1;

    INSERT INTO hrms.Holidays
        (TenantId, CompanyId, HolidayName, HolidayDate, Description, IsOptional)
    OUTPUT INSERTED.HolidayId, INSERTED.CompanyId, INSERTED.HolidayName,
           INSERTED.HolidayDate, INSERTED.Description, INSERTED.IsOptional,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    VALUES
        (@TenantId, @CompanyId, @HolidayName, @HolidayDate, @Description, @IsOptional);
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Holidays_Update]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @HolidayId uniqueidentifier,
    @HolidayName nvarchar(150),
    @HolidayDate date,
    @Description nvarchar(500) = NULL,
    @IsOptional bit,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Holidays
    SET HolidayName = @HolidayName,
        HolidayDate = @HolidayDate,
        Description = @Description,
        IsOptional = @IsOptional,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.HolidayId, INSERTED.CompanyId, INSERTED.HolidayName,
           INSERTED.HolidayDate, INSERTED.Description, INSERTED.IsOptional,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND HolidayId = @HolidayId AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Holidays_Deactivate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @HolidayId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Holidays
    SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND HolidayId = @HolidayId AND RowVersion = @RowVersion
      AND IsActive = 1 AND DeletedAtUtc IS NULL;

    SELECT @@ROWCOUNT;
END;
GO
