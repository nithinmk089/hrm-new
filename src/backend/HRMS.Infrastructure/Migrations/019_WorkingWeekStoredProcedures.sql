/*
    Migration: 019_WorkingWeekStoredProcedures
    Purpose:
      - Read company defaults and office overrides.
      - Atomically replace a complete seven-day schedule.
*/
CREATE OR ALTER PROCEDURE [hrms].[usp_WorkingWeek_Get]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OfficeConfigured bit = 0;
    DECLARE @CompanyConfigured bit = 0;
    DECLARE @EffectiveScope varchar(10) = 'Company';

    IF @OfficeId IS NOT NULL
    BEGIN
        SELECT @OfficeConfigured = CASE WHEN COUNT(*) = 7 THEN 1 ELSE 0 END
        FROM hrms.WorkingWeekDays
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND ScopeType = 'Office' AND OfficeId = @OfficeId;
    END;

    SELECT @CompanyConfigured = CASE WHEN COUNT(*) = 7 THEN 1 ELSE 0 END
    FROM hrms.WorkingWeekDays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND ScopeType = 'Company' AND OfficeId IS NULL;

    IF @OfficeId IS NOT NULL AND @OfficeConfigured = 1
        SET @EffectiveScope = 'Office';

    SELECT
        CASE WHEN @EffectiveScope = 'Office' THEN @OfficeConfigured ELSE @CompanyConfigured END AS IsConfigured,
        CASE WHEN @OfficeId IS NOT NULL AND @EffectiveScope = 'Company' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsInherited;

    SELECT DayOfWeek, IsWorkingDay
    FROM hrms.WorkingWeekDays
    WHERE TenantId = @TenantId AND CompanyId = @CompanyId
      AND ScopeType = @EffectiveScope
      AND ((@EffectiveScope = 'Company' AND OfficeId IS NULL)
        OR (@EffectiveScope = 'Office' AND OfficeId = @OfficeId))
    ORDER BY DayOfWeek;
END;
GO

CREATE OR ALTER PROCEDURE [hrms].[usp_WorkingWeek_Save]
    @TenantId uniqueidentifier,
    @CompanyId uniqueidentifier,
    @OfficeId uniqueidentifier = NULL,
    @Day1 bit, @Day2 bit, @Day3 bit, @Day4 bit, @Day5 bit, @Day6 bit, @Day7 bit
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
        THROW 51251, 'The company does not exist, is inactive, or is outside the authenticated tenant.', 1;

    IF @OfficeId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM hrms.Offices
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND OfficeId = @OfficeId AND IsActive = 1 AND DeletedAtUtc IS NULL
    )
        THROW 51252, 'The office does not exist, is inactive, or is outside the authenticated company.', 1;

    BEGIN TRANSACTION;

    IF @OfficeId IS NULL
    BEGIN
        DELETE FROM hrms.WorkingWeekDays
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND ScopeType = 'Company' AND OfficeId IS NULL;

        INSERT INTO hrms.WorkingWeekDays
            (TenantId, CompanyId, ScopeType, OfficeId, DayOfWeek, IsWorkingDay)
        VALUES
            (@TenantId, @CompanyId, 'Company', NULL, 1, @Day1),
            (@TenantId, @CompanyId, 'Company', NULL, 2, @Day2),
            (@TenantId, @CompanyId, 'Company', NULL, 3, @Day3),
            (@TenantId, @CompanyId, 'Company', NULL, 4, @Day4),
            (@TenantId, @CompanyId, 'Company', NULL, 5, @Day5),
            (@TenantId, @CompanyId, 'Company', NULL, 6, @Day6),
            (@TenantId, @CompanyId, 'Company', NULL, 7, @Day7);
    END
    ELSE
    BEGIN
        DELETE FROM hrms.WorkingWeekDays
        WHERE TenantId = @TenantId AND CompanyId = @CompanyId
          AND ScopeType = 'Office' AND OfficeId = @OfficeId;

        INSERT INTO hrms.WorkingWeekDays
            (TenantId, CompanyId, ScopeType, OfficeId, DayOfWeek, IsWorkingDay)
        VALUES
            (@TenantId, @CompanyId, 'Office', @OfficeId, 1, @Day1),
            (@TenantId, @CompanyId, 'Office', @OfficeId, 2, @Day2),
            (@TenantId, @CompanyId, 'Office', @OfficeId, 3, @Day3),
            (@TenantId, @CompanyId, 'Office', @OfficeId, 4, @Day4),
            (@TenantId, @CompanyId, 'Office', @OfficeId, 5, @Day5),
            (@TenantId, @CompanyId, 'Office', @OfficeId, 6, @Day6),
            (@TenantId, @CompanyId, 'Office', @OfficeId, 7, @Day7);
    END;

    COMMIT TRANSACTION;
END;
GO
