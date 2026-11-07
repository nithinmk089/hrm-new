# Local SQL Server development setup

This guide is for a developer-owned local SQL Server instance. It does not configure production.

## Prerequisites

- SQL Server 2022 or later (Developer edition is suitable for local development).
- SQL Server Management Studio (SSMS) or the `sqlcmd` utility.
- Windows Authentication is recommended for local development.

Check the server version before applying migrations:

```sql
SELECT
    @@SERVERNAME AS ServerName,
    SERVERPROPERTY('ProductVersion') AS ProductVersion,
    SERVERPROPERTY('ProductLevel') AS ProductLevel,
    SERVERPROPERTY('Edition') AS Edition;
```

The current database baseline targets SQL Server 2022+ and compatibility level 160.

## 1. Create the development database

Connect to the local instance using SSMS and execute this once:

```sql
IF DB_ID(N'HRMS_Dev') IS NULL
BEGIN
    CREATE DATABASE HRMS_Dev
        COLLATE SQL_Latin1_General_CP1_CI_AS;
END;
GO

ALTER DATABASE HRMS_Dev SET COMPATIBILITY_LEVEL = 160;
GO

SELECT name, compatibility_level, collation_name
FROM sys.databases
WHERE name = N'HRMS_Dev';
GO
```

If your local SQL Server instance is older than SQL Server 2022, do not lower the compatibility level silently. Confirm the supported version and revise the architecture baseline deliberately.

## 2. Apply the current migrations

For the initial local setup, use SSMS:

1. Open `database/migrations/0001_foundation.sql`.
2. Connect to your local SQL Server instance and select `HRMS_Dev` as the query database.
3. Execute the complete script and confirm there are no errors.
4. Repeat for `database/migrations/0002_foundation_procedures.sql`.

The scripts use SQL Server batch separators (`GO`), so execute them with SSMS or `sqlcmd`; do not submit the entire file as one ordinary ADO.NET command.

Example using Windows Authentication from PowerShell (replace the instance name if needed):

```powershell
sqlcmd -S "Nithin\\SQLEXPRESS01" -E -d "HRMS_Dev" -b -i "database/migrations/0001_foundation.sql"
if ($LASTEXITCODE -ne 0) { throw "Migration 0001 failed." }

sqlcmd -S "Nithin\\SQLEXPRESS01" -E -d "HRMS_Dev" -b -i "database/migrations/0002_foundation_procedures.sql"
if ($LASTEXITCODE -ne 0) { throw "Migration 0002 failed." }
```

For your current local instance, use `Nithin\\SQLEXPRESS01`. For a default instance, the server argument may be `.` or `localhost`.

**Migration tracking note:** migration 0001 records itself in `platform.DatabaseVersions`. Migration 0002 is currently tracked by the deployment process after successful execution; until that runner is implemented, do not manually insert a version record before verifying the script has completed successfully.

## 3. Verify the database

Run against `HRMS_Dev`:

```sql
SELECT VersionNumber, Description, AppliedAtUtc, AppliedBy
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

SELECT PermissionCode, PermissionName, ModuleCode
FROM security.Permissions
ORDER BY ModuleCode, PermissionCode;
```

Expected foundation schemas: `platform`, `security`, and `audit`.

Expected migration 0001 tables: `DatabaseVersions`, `Tenants`, `Configurations`, `Permissions`, `Users`, `Roles`, `UserRoles`, `RolePermissions`, `RefreshTokens`, `AuditLogs`, and `ErrorLogs`.

Expected migration 0002 procedures: the four procedures listed in the verification query, plus the initial permission catalog.

## 4. Local connection string

When the API project is created, configure the connection string with .NET User Secrets rather than committing credentials:

```powershell
dotnet user-secrets init --project src/backend/HRMS.API
dotnet user-secrets set "ConnectionStrings:Hrms" "Server=localhost;Database=HRMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;" --project src/backend/HRMS.API
```

For your current instance, use `Server=Nithin\\SQLEXPRESS01`. Change this if your SQL Server instance name changes. Use `TrustServerCertificate=True` only for local development; configure trusted certificates for deployed environments.

Do not use a SQL Server administrator account as the API runtime identity. Before connecting the API, create a dedicated local application login/user with only the required procedure execution permissions. The migration identity and application identity must remain separate.

## 5. What is not yet validated

No migration has been executed against your local SQL Server by this repository workflow yet. After you apply the scripts, share the SQL verification output (omit any sensitive server details) so we can confirm the schema and procedures before adding the migration runner and database integration tests.
