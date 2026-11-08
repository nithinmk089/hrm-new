# Authorization Foundation (V1)

## Canonical data model

Authorization uses only the canonical `security` schema established by the
platform foundation and tenant authorization migrations:

- `security.Users`: global user identity and enabled/deleted state.
- `security.UserTenantMemberships`: tenant membership and membership status.
- `security.Roles`: tenant-owned role definitions.
- `security.Permissions`: global permission catalogue. Existing permission
  IDs and rows must be preserved.
- `security.RolePermissions`: tenant-scoped role-to-permission grants.
- `security.UserRoleAssignments`: tenant/company/office scoped role assignments.
- `hrms.Companies`: tenant-scoped company hierarchy.
- `platform.Tenants`: tenant lifecycle state.

Do not use deprecated `hrms.Roles`, `hrms.Permissions`, `hrms.UserRoles`,
or `hrms.RolePermissions` tables for new application code.

## Permission evaluation

A permission is granted only when all of the following are true:

1. The user is enabled and not deleted.
2. The exact membership ID belongs to the user and selected tenant.
3. The membership is Active.
4. The tenant is Active and not deleted.
5. A non-expired role assignment exists.
6. The assigned role is active.
7. The role has the requested active permission in the same tenant.
8. The assignment scope covers the requested resource scope.

Supported scope behavior:

- Tenant assignment: covers the tenant.
- Company assignment: covers the assigned company.
- Company assignment with `IncludeDescendants = 1`: also covers descendant
  companies in the same tenant.
- Office assignment: covers only its exact company and office.

A company-level assignment does not automatically inherit into subsidiaries.
Office-level assignments do not grant access to the whole company.

## Trusted identity context

UserId, TenantId and MembershipId must be derived from the validated JWT claims.
Never accept these identifiers as trusted values from a request body or query
string. CompanyId and OfficeId identify the resource being accessed and must
be resolved and validated against tenant-owned resource records by the API or
module service.

## Current implementation increment

The application exposes `IPermissionAuthorizationService`. The SQL repository
checks membership, tenant, role, permission and scope together on every check.
This avoids relying on stale permission claims embedded in a long-lived token.

The current increment provides the evaluation service and persistence query.
ASP.NET Core authorization policies/handlers and endpoint integration are a
separate follow-up increment. Module endpoints must remain protected until
those policies are wired.
