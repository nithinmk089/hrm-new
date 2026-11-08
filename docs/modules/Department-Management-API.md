# Department Management API

Department records are owned by a company and are always queried through the authenticated tenant context. Clients must not send a TenantId; the API derives it from the validated JWT principal.

## Routes

All routes require a valid access token and the listed permission.

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/api/v1/companies/{companyId}/departments` | `departments.read` | List non-deleted departments |
| GET | `/api/v1/companies/{companyId}/departments/{departmentId}` | `departments.read` | Get one department |
| POST | `/api/v1/companies/{companyId}/departments` | `departments.create` | Create a department |
| PUT | `/api/v1/companies/{companyId}/departments/{departmentId}` | `departments.update` | Update department name, description and parent |
| PATCH | `/api/v1/companies/{companyId}/departments/{departmentId}/deactivate` | `departments.deactivate` | Deactivate using optimistic concurrency |

Authorization evaluates the route company against tenant/company/office role assignments. Database procedures also include TenantId and CompanyId predicates.

## Create request

```json
{
  "departmentCode": "FINANCE",
  "departmentName": "Finance",
  "description": "Finance and accounting",
  "parentDepartmentId": null
}
```

## Update request

```json
{
  "departmentName": "Finance & Accounts",
  "description": "Finance and accounting",
  "parentDepartmentId": null,
  "rowVersion": "AAAAAAAAB9E="
}
```

The `rowVersion` value is the Base64 representation of the 8-byte value returned by GET or POST. A missing department returns 404. If the department exists but the submitted rowversion is stale, the API returns 409 Conflict.

## Behavior and validation

- Department codes are trimmed and normalized to uppercase; codes are unique within a company while the record is not soft-deleted.
- Names are required and limited to 150 characters. Descriptions are optional and limited to 500 characters.
- A parent must be active and belong to the same company.
- A department cannot parent itself or one of its descendants; database update logic rejects hierarchy cycles.
- Reads exclude soft-deleted rows and include both active and inactive rows.
- Deactivation is a logical state change; it does not delete the row.
- An update or deactivate with a stale rowversion does not overwrite a concurrent change.

## Database implementation

Migration `009_DepartmentStoredProcedures.sql` adds the tenant-scoped Dapper procedure contract under the `hrms` schema. Migration `008_OrganizationMasterPermissions.sql` adds the department, office and designation permission codes.
