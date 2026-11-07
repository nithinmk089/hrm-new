# Database migrations

SQL Server schema changes are maintained as ordered SQL scripts.

## Applying migrations

1. Create the HRMS application database using the approved deployment configuration.
2. Confirm SQL Server 2022+ and compatibility level 160.
3. Review the migration in a non-production environment.
4. Execute migrations in numeric order using a dedicated migration identity.
5. Verify the corresponding row in `platform.DatabaseVersions`.
6. Run database integration tests before promoting the release.

Do not edit a migration after it has been applied to a shared environment. Create a new corrective migration instead.

## Current migrations

| Version | File | Scope |
|---|---|---|
| 0001 | `0001_foundation.sql` | Tenant, user, role, permission, refresh-token, configuration, audit and error-log foundation |

## Important

- Application connections must use a least-privilege database principal.
- Do not grant ordinary application identities UPDATE/DELETE permissions on audit history.
- Never commit real credentials or production data.
- The application must derive tenant context from authenticated server-side identity and validate it for every use case.
