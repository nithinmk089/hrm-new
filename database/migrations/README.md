# Database migrations

SQL Server schema changes are maintained as ordered SQL scripts.

## Applying migrations

Use the repository migration runner from the repository root. It reads the connection string from the `HRMS_CONNECTION_STRING` environment variable, applies scripts in filename order, understands standalone `GO` batch separators, and records successful versions in `platform.DatabaseVersions`.

```powershell
$env:HRMS_CONNECTION_STRING = "Server=Nithin\SQLEXPRESS01;Database=HRMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run --project src/tools/HRMS.DatabaseMigrator
```

For local development, Windows Authentication is preferred. Do not store connection strings containing credentials in source control. `TrustServerCertificate=True` is for local development only.

The runner:
- Requires filenames in the format `NNNN_description.sql`.
- Applies migrations in ordinal filename order.
- Skips versions already recorded.
- Calculates SHA-256 checksums for newly applied migrations and rejects later changes to checksummed scripts.
- Uses a SQL Server application lock to prevent two runner instances from applying migrations concurrently.
- Executes each `GO`-separated batch sequentially and records the version only after all batches complete.
- Does not automatically wrap an entire migration in a transaction. A migration must manage its own transaction where atomic DDL/DML is required and should be safe to retry after a partial failure.

The runner supports standalone `GO` lines, including an optional trailing SQL comment. Do not use `GO n` repeat counts in migration files.

## Current migrations

| Version | File | Scope |
|---|---|---|
| 0001 | `0001_foundation.sql` | Tenant, user, role, permission, refresh-token, configuration, audit and error-log foundation |
| 0002 | `0002_foundation_procedures.sql` | Database version/configuration reads, audit/error inserts and initial permission catalog |

Migration 0001 creates the version table and records version 0001. Migration 0002 is re-runnable: procedures use `CREATE OR ALTER` and permission seeds are guarded by `IF NOT EXISTS`. If 0002 was previously executed manually, the runner can safely execute it once more and then record its version.

## Operational rules

- Do not edit a migration after it has been applied to a shared environment. Create a new corrective migration instead.
- Application connections must use a least-privilege database principal.
- Do not grant ordinary application identities UPDATE/DELETE permissions on audit history.
- Never commit real credentials or production data.
- Derive tenant context from authenticated server-side identity and validate it for every use case.
- Tenant-owned procedures must scope all reads and writes by trusted TenantId.
