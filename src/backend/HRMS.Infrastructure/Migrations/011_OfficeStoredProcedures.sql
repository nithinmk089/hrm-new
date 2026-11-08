/*
    Migration: 011_OfficeStoredProcedures
    Purpose:
      - Add tenant/company-scoped office persistence procedures.
      - Enforce active company ownership and unique active head office.
      - Preserve optimistic concurrency through RowVersion.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_Offices_GetAll]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OfficeId, CompanyId, OfficeCode, OfficeName, AddressLine1, AddressLine2,
           City, StateOrProvince, PostalCode, CountryCode, TimeZoneId, IsHeadOffice,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Offices
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND DeletedAtUtc IS NULL
    ORDER BY IsHeadOffice DESC, OfficeName, OfficeCode;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Offices_GetById]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OfficeId, CompanyId, OfficeCode, OfficeName, AddressLine1, AddressLine2,
           City, StateOrProvince, PostalCode, CountryCode, TimeZoneId, IsHeadOffice,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Offices
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND OfficeId = @OfficeId
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Offices_GetByCode]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeCode nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OfficeId, CompanyId, OfficeCode, OfficeName, AddressLine1, AddressLine2,
           City, StateOrProvince, PostalCode, CountryCode, TimeZoneId, IsHeadOffice,
           IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
    FROM hrms.Offices
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND OfficeCode = @OfficeCode
      AND DeletedAtUtc IS NULL;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Offices_Create]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeCode nvarchar(30),
    @OfficeName nvarchar(150),
    @AddressLine1 nvarchar(200) = NULL,
    @AddressLine2 nvarchar(200) = NULL,
    @City nvarchar(100) = NULL,
    @StateOrProvince nvarchar(100) = NULL,
    @PostalCode nvarchar(20) = NULL,
    @CountryCode char(2),
    @TimeZoneId nvarchar(100) = NULL,
    @IsHeadOffice bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1 FROM hrms.Companies
        WHERE TenantId = @TenantId
          AND CompanyId = @CompanyId
          AND IsActive = 1
          AND DeletedAtUtc IS NULL
    )
        THROW 51211, 'The company does not exist, is inactive, or is outside the authenticated tenant.', 1;

    INSERT INTO hrms.Offices
    (
        TenantId, CompanyId, OfficeCode, OfficeName, AddressLine1, AddressLine2,
        City, StateOrProvince, PostalCode, CountryCode, TimeZoneId, IsHeadOffice
    )
    OUTPUT INSERTED.OfficeId, INSERTED.CompanyId, INSERTED.OfficeCode,
           INSERTED.OfficeName, INSERTED.AddressLine1, INSERTED.AddressLine2,
           INSERTED.City, INSERTED.StateOrProvince, INSERTED.PostalCode,
           INSERTED.CountryCode, INSERTED.TimeZoneId, INSERTED.IsHeadOffice,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc,
           INSERTED.RowVersion
    VALUES
    (
        @TenantId, @CompanyId, @OfficeCode, @OfficeName, @AddressLine1, @AddressLine2,
        @City, @StateOrProvince, @PostalCode, @CountryCode, @TimeZoneId, @IsHeadOffice
    );
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Offices_Update]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeId uniqueidentifier,
    @OfficeName nvarchar(150),
    @AddressLine1 nvarchar(200) = NULL,
    @AddressLine2 nvarchar(200) = NULL,
    @City nvarchar(100) = NULL,
    @StateOrProvince nvarchar(100) = NULL,
    @PostalCode nvarchar(20) = NULL,
    @CountryCode char(2),
    @TimeZoneId nvarchar(100) = NULL,
    @IsHeadOffice bit,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Offices
    SET OfficeName = @OfficeName,
        AddressLine1 = @AddressLine1,
        AddressLine2 = @AddressLine2,
        City = @City,
        StateOrProvince = @StateOrProvince,
        PostalCode = @PostalCode,
        CountryCode = @CountryCode,
        TimeZoneId = @TimeZoneId,
        IsHeadOffice = @IsHeadOffice,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.OfficeId, INSERTED.CompanyId, INSERTED.OfficeCode,
           INSERTED.OfficeName, INSERTED.AddressLine1, INSERTED.AddressLine2,
           INSERTED.City, INSERTED.StateOrProvince, INSERTED.PostalCode,
           INSERTED.CountryCode, INSERTED.TimeZoneId, INSERTED.IsHeadOffice,
           INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc,
           INSERTED.RowVersion
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND OfficeId = @OfficeId
      AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL
      AND (IsActive = 1 OR @IsHeadOffice = 0);
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_Offices_Deactivate]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeId uniqueidentifier,
    @RowVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE hrms.Offices
    SET IsActive = 0,
        IsHeadOffice = 0,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND CompanyId = @CompanyId
      AND OfficeId = @OfficeId
      AND RowVersion = @RowVersion
      AND DeletedAtUtc IS NULL;

    SELECT @@ROWCOUNT;
END;
GO
