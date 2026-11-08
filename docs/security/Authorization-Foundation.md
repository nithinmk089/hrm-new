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

## Trusted identity and resource context

UserId, TenantId and MembershipId are derived only from the validated JWT
principal claims `sub`, `tenant_id` and `membership_id`. They are never read
from a request body, query string or arbitrary header.

The API accepts resource identifiers from route values named `companyId` and
`officeId`. These values are treated only as resource selectors, not as proof
of access. The permission repository checks the assignment against the
tenant-scoped company/office records. An office route without a company route
is denied.

## ASP.NET Core integration

Protected endpoints can now declare policies using the stable permission
catalogue codes:

```csharp
app.MapGet("/api/v1/companies/{companyId:guid}", GetCompany)
    .RequireAuthorization("Permission:companies.read");
```

The dynamic policy provider requires an authenticated principal. The handler
extracts the three identity claims, reads optional route scope, and delegates
the decision to `IPermissionAuthorizationService`. Missing or malformed
identity/scope context is denied. The database evaluator is called on each
authorization decision, so role or membership changes are not dependent on
stale permission claims embedded in the access token.

Do not add `TenantId`, `UserId`, `MembershipId`, role or permission inputs
to endpoint request DTOs. Do not expose module endpoints until each operation
has an appropriate permission policy and its repository enforces tenant scope.
