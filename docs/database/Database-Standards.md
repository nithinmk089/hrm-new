# HRMS database standards

These standards apply to new SQL Server objects introduced through versioned
scripts under `src/backend/HRMS.Infrastructure/Migrations`.

## 1. Schema and naming

- Place application tables in the `hrms` schema.
- Use PascalCase for table and column names; use plural table names.
- Use explicit, descriptive names for constraints and indexes.
- Prefix primary-key constraints with `PK_`, foreign keys with `FK_`,
  unique constraints with `UQ_`, check constraints with `CK_`, and
  default constraints with `DF_`.
- Prefix indexes with `IX_` or `UX_` for unique indexes.

## 2. Keys and relationships

- Use `uniqueidentifier` primary keys with `NEWSEQUENTIALID()` defaults.
- Company-owned records must include `CompanyId`.
- Foreign keys from company-owned records must preserve the company boundary.
  For relationships between two company-owned records, prefer composite
  foreign keys that include `CompanyId`.
- Do not use cascading deletes for HR records. Use explicit lifecycle operations.

## 3. Audit and lifecycle

Standard audit columns for mutable business tables:

- `CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`
- `CreatedByUserId uniqueidentifier NULL`
- `UpdatedAtUtc datetime2(7) NULL`
- `UpdatedByUserId uniqueidentifier NULL`
- `DeletedAtUtc datetime2(7) NULL`
- `DeletedByUserId uniqueidentifier NULL`
- `RowVersion rowversion NOT NULL`

A non-null `DeletedAtUtc` means the record is soft-deleted. Normal application
queries must exclude soft-deleted records unless an administrative or audit
workflow explicitly needs them. Use filtered unique indexes on
`DeletedAtUtc IS NULL` when uniqueness should be released after deletion.

Audit user IDs remain nullable until the identity/user module is introduced.
Application services must populate audit values from the authenticated actor;
clients must not be allowed to set these fields directly.

## 4. Data types and time

- Store timestamps in UTC using `datetime2(7)`; use `SYSUTCDATETIME()`
  for database-generated defaults.
- Store calendar dates (for example, a leave date) as `date`, not as a UTC
  timestamp.
- Use `bit` for boolean flags.
- Use `decimal(p,s)` for exact quantities and monetary values; never use
  `float` for money.
- Use ISO 3166-1 alpha-2 country codes and ISO 4217 currency codes where
  applicable.
- Keep SQL Server `rowversion` values as opaque concurrency tokens.

## 5. Company isolation

The `Companies` table is the company/tenant root for V1 data. Every
company-owned record carries `CompanyId`. API authorization and repository
queries must derive the permitted CompanyId from the authenticated server-side
context, not trust a CompanyId supplied by the browser. Database foreign keys
reinforce relational integrity but do not replace authorization or tenant
filtering.

## 6. Migration discipline

- Name scripts `NNN_Description.sql`, using a unique increasing number.
- Never edit or remove a migration after it has been applied in a shared
  environment.
- Apply schema changes only through the migration runner.
- Keep each migration focused and reviewable.
- Test migrations against a disposable development database before release.
