# Local SQL Server development setup

This guide uses the detected local SQL Server Express instance. It does not configure production.

## Detected environment

- Instance: `Nithin\SQLEXPRESS01`
- Edition: SQL Server Express (64-bit)
- Version: `16.0.1000.6` (SQL Server 2022)
- Server collation: `Latin1_General_CI_AS`
- HRMS database collation: `SQL_Latin1_General_CP1_CI_AS`

The server collation and database collation may differ. The HRMS database is explicitly created with the database collation required by the project standards.

## 1. Create the development database

Connect to `Nithin\SQLEXPRESS01` using SSMS and execute once:

```sql
IF DB_ID(N'HRMS_Dev') IS NULL
BEGIN
    CREATE DATABASE HRMS_Dev
        COLLATE SQL_Latin1_General_CP1_CI_AS;
END;
GO

ALTER DATABASE HRMS_Dev SET COMPATIBILITY_LEVEL = 160;
GO
```

## 2. Run database migrations

From the repository root, use PowerShell:

```powershell
$env:HRMS_CONNECTION_STRING = "Server=Nithin\SQLEXPRESS01;Database=HRMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run --project src/tools/HRMS.DatabaseMigrator
```

The runner applies migrations in order, handles standalone `GO` batch separators, records successful versions, skips already recorded versions and detects changes to scripts that have a recorded checksum.

The runner is safe to use against the current database where migration 0001 is recorded and migration 0002 was executed manually but not recorded: it will skip 0001, re-run the idempotent 0002 script and record version 0002.

## 3. Verify the database

Run against `HRMS_Dev`:

```sql
SELECT VersionNumber, Description, AppliedAtUtc, AppliedBy, ScriptChecksum
FROM platform.DatabaseVersions
ORDER BY VersionNumber;

SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name;

SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS ProcedureName
FROM sys.procedures
WHERE name IN
(
    N'usp_DatabaseVersions_GetLatest',
    N'usp_Configurations_Get',
    N'usp_AuditLogs_Insert',
    N'usp_ErrorLogs_Insert'
)
ORDER BY SchemaName, ProcedureName;

SELECT COUNT(*) AS TotalPermissions
FROM security.Permissions;
```

Expected results:
- 11 foundation tables across `platform`, `security`, and `audit`.
- Four foundation stored procedures.
- 11 initial permissions.
- Migration versions `0001` and `0002` recorded after the runner completes.

## 4. Local API connection configuration

When the API project is created, configure its connection string with .NET User Secrets:

```powershell
dotnet user-secrets init --project src/backend/HRMS.API
dotnet user-secrets set "ConnectionStrings:Hrms" "Server=Nithin\SQLEXPRESS01;Database=HRMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;" --project src/backend/HRMS.API
```

Do not use a SQL Server administrator account as the API runtime identity. The migration identity and application identity must remain separate. `TrustServerCertificate=True` is for local development only.

## 5. Limitations and validation

SQL Server Express is suitable for local development, but has a 10 GB maximum relational database size. No production deployment configuration is implied by this guide.

The repository runner has not yet been executed in your local environment. Run it and share the console output plus the verification query results before we mark automated migration execution as validated.
