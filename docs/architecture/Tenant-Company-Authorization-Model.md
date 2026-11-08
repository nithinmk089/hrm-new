# HRMS V1 tenant, company and authorization model

Status: Approved architectural baseline for HRMS V1.

## 1. Core organizational concepts

- **Tenant**: the customer account / organization group using the HRMS platform.
- **Company**: a legal entity belonging to one tenant.
- **Parent company**: an optional legal-company relationship within the same tenant.
- **Office**: a physical or operational location belonging to one legal company.
- **Employee**: an employee record belonging to a legal company. Effective-dated employment assignments capture office, department and designation changes.

A tenant can contain one or more legal companies. A company can contain multiple offices. A company is not created merely because an office is in another city or country; a distinct legal entity is represented as a separate company.

## 2. Tenant isolation

Every tenant-owned business record must be scoped to a TenantId, directly or through a parent record whose tenant relationship is enforced by foreign keys.

The authenticated tenant context is resolved server-side. API clients must not be trusted to choose an arbitrary TenantId. Repository queries must always apply tenant scope. Database constraints provide an additional integrity boundary but do not replace application authorization.

## 3. Company hierarchy

hrms.Companies will include TenantId (required), ParentCompanyId (nullable), a unique composite key on (TenantId, CompanyId), and a self-referencing composite foreign key (TenantId, ParentCompanyId) to (TenantId, CompanyId).

This ensures a parent company cannot belong to another tenant. Application validation must also prevent hierarchy cycles.

hrms.Offices remains company-owned. An office cannot be shared by two legal companies.

## 4. Identity, roles and permissions

- A user identity is global and can be associated with multiple tenants.
- Tenant membership determines which tenant accounts the user can enter.
- Roles are tenant-defined.
- Permissions are a central catalogue of stable permission codes.
- Role-permission mappings grant capabilities.
- Role assignments grant those capabilities only within a defined organizational scope.

The existing security.Permissions catalogue is canonical and its existing records/IDs must be preserved. The existing audit.AuditLogs table is the canonical audit store. Do not create a second active permission catalogue or audit log.

## 5. Access scopes

V1 supports three scopes:

| Scope | Meaning |
|---|---|
| Tenant | All companies and offices in the tenant |
| Company | One company and its offices |
| Office | One office |

A role assignment carries a scope and optional IncludeDescendants setting. Descendant-company access is opt-in. It applies only to company-scoped assignments and expands access to child companies in the same tenant. Tenant-scoped assignments already cover all companies and do not need inheritance.

The safe default is least privilege: no implicit subsidiary inheritance, no implicit access to sensitive employee data, and no implicit write access from a read/reporting role.

Permissions and scope are evaluated together. For example, employees.read plus tenant scope permits employee reads across that tenant, while employees.documents.read must be separately granted for document access.

## 6. CEO and HR examples

- Group CEO: tenant-scoped role, typically read/reporting permissions across all companies.
- Group HR Head: tenant-scoped HR role, with operational permissions explicitly assigned.
- Company CEO: company-scoped role; subsidiary access only when explicitly enabled.
- Company HR Manager: company-scoped HR role.
- Branch HR Manager: office-scoped HR role.

Sensitive compensation, bank, identity-document and employee-document data require separate permissions and auditing.

## 7. Migration safety and canonical tables

Migrations 001-004 have already been applied in the development environment and must not be edited. Migration 005 establishes tenant ownership, global identity, memberships, scoped role assignments, and canonical audit relationships. It preserves the existing permission catalogue and does not drop the legacy duplicate HRMS identity/security tables. Those tables remain deprecated until application code has been migrated and a later cleanup migration is reviewed.

Before authoring Migration 005, inspect the exact existing definitions, indexes, constraints, foreign keys, row counts and applied migration ledger for:

- platform.Tenants
- security.Users, security.Roles, security.Permissions, security.UserRoles, security.RolePermissions, security.RefreshTokens
- audit.AuditLogs
- Duplicate hrms identity/security/audit tables
- hrms.Companies and hrms.Offices

The read-only inspection script is docs/database/005_PreMigrationSchemaInspection.sql.

Migration 005 is implemented as `005_TenantIdentityAndAuthorizationFoundation.sql`. It checks the inspected permission count, blocks if users or companies unexpectedly contain rows, preserves all 11 permission IDs, uses named constraints, and runs through the existing transactional migration runner. The inspected development database reported zero rows in all relevant tables except `security.Permissions` (11 rows). The migration must still be executed and verified locally before application development continues.

## 8. Application implementation implications

- Company list/get/create/update/deactivate operations must require a tenant context.
- Company repository SQL must include TenantId filters.
- Company create requests must not accept TenantId from an untrusted client; the server derives it from the authenticated context.
- Employee and other module repositories must enforce both tenant and company scope.
- Access checks must be centralized and reused across modules.
- Do not expose company or employee APIs without tenant and permission authorization.