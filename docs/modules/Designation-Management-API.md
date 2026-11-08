# Designation Management API

## Scope

Designation records are company-scoped organization masters. Every request is scoped to the authenticated tenant and the company in the route. The API does not accept TenantId from the request body or query string.

## Routes

Base route: `/api/v1/companies/{companyId}/designations`

| Method | Route | Permission | Success |
|---|---|---|---|
| GET | `/api/v1/companies/{companyId}/designations` | `designations.read` | 200 |
| GET | `/api/v1/companies/{companyId}/designations/{designationId}` | `designations.read` | 200 |
| POST | `/api/v1/companies/{companyId}/designations` | `designations.create` | 201 |
| PUT | `/api/v1/companies/{companyId}/designations/{designationId}` | `designations.update` | 200 |
| PATCH | `/api/v1/companies/{companyId}/designations/{designationId}/deactivate` | `designations.deactivate` | 204 |

Missing records return 404. Invalid input returns 400. Duplicate active designation codes and stale updates return 409. Deactivation returns 409 when the record is missing, already inactive, or its RowVersion is stale.

## Create request

```json
{
  "designationCode": "SENIOR-DEV",
  "designationName": "Senior Developer",
  "description": "Senior engineering role",
  "jobLevel": 4
}
```

## Update request

The code is immutable after creation. Supply the current eight-byte RowVersion as a Base64 JSON string.

```json
{
  "designationName": "Lead Developer",
  "description": "Engineering team lead",
  "jobLevel": 5,
  "rowVersion": "AAAAAAAAB9E="
}
```

## Deactivate request

```json
{
  "rowVersion": "AAAAAAAAB9E="
}
```

## Validation and persistence

- DesignationCode is required, trimmed, uppercased, and limited to 30 characters.
- DesignationName is required, trimmed, and limited to 150 characters.
- Description is optional and limited to 500 characters.
- JobLevel is optional; when supplied it must be a positive small integer.
- DesignationCode is unique within a company among non-deleted records.
- TenantId and CompanyId predicates are enforced by every stored procedure.
- Creation requires an active company belonging to the authenticated tenant.
- Updates use RowVersion optimistic concurrency.
- Deactivation is a logical state change (IsActive = 0); it does not delete the record.
- Data access uses Dapper and stored procedures only.

## Migration

Apply migration `012_DesignationStoredProcedures.sql` after migrations 001–011:

```powershell
dotnet run --project .\src\backend\HRMS.API\HRMS.API.csproj -- --migrate
```

The permission catalogue entries are already established by migration 008. Development seed grants active permissions to the development administrator according to the existing seed policy.
