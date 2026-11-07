# HRMS V1 — SQL Server Standards

## 1. Platform

- Microsoft SQL Server 2022 or later.
- Database compatibility level 160 or later.
- Default collation: `SQL_Latin1_General_CP1_CI_AS`, unless deployment requirements justify a documented alternative.
- Use schemas to group database responsibilities, for example `system`, `company`, `employee`, `leave`, `timesheet` and `security`.

## 2. Data access strategy

The V1 baseline is **Dapper + stored procedures**. Application code calls parameterized stored procedures through repository interfaces. Do not mix EF Core-generated schema management into this baseline without an approved architecture decision.

SQL source files are the authoritative schema/procedure definitions. Apply them through an ordered, versioned migration process in deployment automation.

## 3. Tenant isolation

- Every tenant-owned business table must contain a non-null `TenantID`.
- Include TenantID in unique constraints and indexes where uniqueness is tenant-scoped.
- Every read, update and delete procedure must scope by TenantID.
- Inserts must obtain TenantID from trusted application context and validate it; never accept an untrusted client-supplied tenant identifier as authority.
- Consider row-level security as defense in depth only after its connection/session context behavior is designed and tested.
- Add automated cross-tenant access tests for every module.

## 4. Standard columns

For mutable tenant-owned business tables, use the applicable subset of:

- `TenantID` — tenant identifier, non-null.
- `CreatedAtUtc`, `CreatedBy`.
- `UpdatedAtUtc`, `UpdatedBy`.
- `IsDeleted`, `DeletedAtUtc`, `DeletedBy` — where soft deletion is appropriate.
- `VersionNo` — optimistic concurrency/version value where appropriate.

Use `datetime2` for timestamps, `date` for business dates, `decimal(p,s)` for exact numeric quantities and `uniqueidentifier` or a documented key strategy for identifiers. Avoid storing dates as strings.

## 5. Naming

- Tables: singular or plural consistently within a schema; recommended plural nouns (for example, `employee.Employees`).
- Primary keys: `<EntityName>Id`.
- Foreign keys: `<ReferencedEntityName>Id`.
- Procedures: `<schema>.usp_<Entity>_<Action>`, e.g. `employee.usp_Employees_GetById`.
- Indexes: `IX_<Table>_<KeyColumns>`; unique indexes begin `UX_`.
- Constraints: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`.
- Use descriptive names; avoid reserved words and unexplained abbreviations.

## 6. Query and procedure rules

- Never use `SELECT *`.
- Always specify columns and schema-qualify object names.
- Use parameterized inputs with appropriate SQL types and lengths.
- Apply TenantID and authorization-relevant scope in the query itself.
- Validate paging bounds and sort/filter identifiers against allowlists.
- Use `SET NOCOUNT ON` in stored procedures.
- Use `TRY...CATCH` where it adds reliable rollback/error handling; rethrow errors with `THROW`.
- Keep procedures focused and document result-set contracts.
- Do not use triggers for application workflows. Document any trigger used for an essential integrity requirement.
- Avoid cursors and scalar UDFs in high-volume query paths unless measured and justified.

## 7. Transactions and concurrency

- Keep transactions short.
- Use explicit transaction boundaries for multi-table business operations.
- Use `SET XACT_ABORT ON` where appropriate.
- Use a rowversion column or explicit VersionNo comparison for optimistic concurrency.
- Return a clear conflict result when a stale version is submitted.
- Design leave balance reservation/consumption and timesheet approval transitions to be atomic and idempotent.

## 8. Indexing and performance

- Create indexes from measured access patterns, not speculatively.
- Include TenantID in leading key positions for common tenant-scoped access paths when appropriate.
- Index foreign keys and common filter/sort combinations after reviewing query plans.
- Avoid functions on indexed columns in predicates when they prevent index seeks.
- Avoid leading-wildcard searches on large tables; use a suitable search strategy if needed.
- Review actual execution plans and logical reads for high-volume endpoints.

## 9. Audit and retention

- Maintain an append-oriented audit history for sensitive changes.
- Do not soft-delete audit logs.
- Minimize sensitive data captured in audit payloads; never store credentials or tokens.
- Define retention, archival and purge policies with applicable legal/business requirements before production.
- Keep operational error logs separate from business audit history.

## 10. Database deployment

- Store ordered scripts under `database/migrations/` and reusable procedure scripts under `database/procedures/`.
- Each migration must be reviewed, repeatable in a clean environment and safe to apply once.
- Record applied migration identifiers/checksums in a schema-version table.
- Do not edit a migration already applied to a shared environment; add a corrective migration.
- Include rollback guidance where a safe rollback is possible.
- Run migrations through a controlled deployment identity with least privilege.
