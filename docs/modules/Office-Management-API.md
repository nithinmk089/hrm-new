# Office Management API

Office records are owned by a company and are always queried through the authenticated tenant context. Clients must not send a TenantId; the API derives it from the validated JWT principal.

## Routes

All routes require a valid access token and the listed permission.

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/api/v1/companies/{companyId}/offices` | `offices.read` | List non-deleted offices |
| GET | `/api/v1/companies/{companyId}/offices/{officeId}` | `offices.read` | Get one office |
| POST | `/api/v1/companies/{companyId}/offices` | `offices.create` | Create an office |
| PUT | `/api/v1/companies/{companyId}/offices/{officeId}` | `offices.update` | Update office details |
| PATCH | `/api/v1/companies/{companyId}/offices/{officeId}/deactivate` | `offices.deactivate` | Deactivate using optimistic concurrency |

Authorization evaluates the route company against tenant/company/office role assignments. Database procedures also include TenantId and CompanyId predicates.

## Create request

```json
{
  "officeCode": "HQ",
  "officeName": "Head Office",
  "addressLine1": "1 Main Street",
  "addressLine2": null,
  "city": "Kochi",
  "stateOrProvince": "Kerala",
  "postalCode": "682001",
  "countryCode": "IN",
  "timeZoneId": "Asia/Kolkata",
  "isHeadOffice": true
}
```

## Update request

The update request contains the editable office fields and the Base64 `rowVersion` returned by GET or POST. OfficeCode is immutable after creation.

```json
{
  "officeName": "Kochi Corporate Office",
  "addressLine1": "2 Main Street",
  "addressLine2": null,
  "city": "Kochi",
  "stateOrProvince": "Kerala",
  "postalCode": "682001",
  "countryCode": "IN",
  "timeZoneId": "Asia/Kolkata",
  "isHeadOffice": true,
  "rowVersion": "AAAAAAAAB9E="
}
```

## Behavior and validation

- Office codes are trimmed and normalized to uppercase; codes are unique within a company while the record is not soft-deleted.
- Office names are required and limited to 150 characters.
- Address lines are limited to 200 characters; city and state/province to 100; postal code to 20; time-zone ID to 100.
- CountryCode must contain two uppercase ASCII letters.
- At most one office per company may be marked as the active head office. The database's filtered unique index remains the final concurrency-safe enforcement point.
- Reads exclude soft-deleted rows and include both active and inactive records.
- Deactivation is a logical state change. It also clears IsHeadOffice so the company can designate another head office.
- An update with a stale rowversion returns 409 Conflict. A missing office returns 404.
- OfficeCode is not editable through PUT.

## Database implementation

Migration `011_OfficeStoredProcedures.sql` adds the tenant-scoped Dapper procedure contract under the `hrms` schema. Migration `008_OrganizationMasterPermissions.sql` provides the office permission codes.
