# Database migrations

SQL Server schema changes are maintained as ordered SQL scripts.

## Applying migrations

1. Create the HRMS application database using the approved deployment configuration.
2. Confirm SQL Server 2022+ and compatibility level 160.
3. Review migrations in a non-production environment.
4. Execute migrations in numeric order using a dedicated migration identity.
5. Verify applied versions in `platform.DatabaseVersions`.
6. Run database integration tests before promoting the release.

Do not edit a migration after it has been applied to a shared environment. Create a new corrective migration instead.

## Current migrations

| Version | File | Scope |
|---|---|---|
| 0001 | `0001_foundation.sql` | Tenant, user, role, permission, refresh-token, configuration, audit and error-log foundation |
| 0002 | `0002_foundation_procedures.sql` | Database version/configuration reads, audit/error inserts and initial permission catalog |

Migration 0001 creates the version table and records version 0001. The deployment runner must record version 0002 only after successful completion of migration 0002.

## Important

- Application connections must use a least-privilege database principal.
- Do not grant ordinary application identities UPDATE/DELETE permissions on audit history.
- Never commit real credentials or production data.
- Derive tenant context from authenticated server-side identity and validate it for every use case.
- Tenant-owned procedures must scope all reads and writes by trusted TenantId.
