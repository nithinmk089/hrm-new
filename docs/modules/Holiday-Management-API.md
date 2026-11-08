# Holiday Management API

## Scope

Holiday Management maintains company-wide holiday dates for HRMS V1. Each
holiday belongs to one authenticated tenant and company. V1 supports one
holiday record per company per calendar date. A multi-day holiday is represented
by one record for each date.

Office-specific calendars, recurring rules, regional calendar imports and
employee-specific overrides are intentionally outside this first version.

## Endpoints

Base route: `/api/v1/companies/{companyId}/holidays`

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/` | `holidays.read` | List company holidays by date |
| GET | `/{holidayId}` | `holidays.read` | Get one holiday |
| POST | `/` | `holidays.create` | Create a holiday |
| PUT | `/{holidayId}` | `holidays.update` | Update a holiday |
| PATCH | `/{holidayId}/deactivate` | `holidays.deactivate` | Deactivate a holiday |

All endpoints require authentication and the relevant permission. TenantId is
derived from the validated access-token claims. CompanyId is route-scoped and
is checked together with TenantId in persistence procedures.

## Request examples

Create:

```json
{
  "holidayName": "New Year's Day",
  "holidayDate": "2027-01-01",
  "description": "New Year public holiday",
  "isOptional": false
}
```

Update requires the current eight-byte RowVersion returned by GET or POST:

```json
{
  "holidayName": "New Year's Day",
  "holidayDate": "2027-01-01",
  "description": "Company holiday",
  "isOptional": false,
  "rowVersion": "AAAAAAAAB9E="
}
```

Deactivate:

```json
{
  "rowVersion": "AAAAAAAAB9E="
}
```

The RowVersion values above are illustrative only; clients must send the value
returned by the API.

## Rules and behavior

- HolidayName is required, trimmed and limited to 150 characters.
- HolidayDate is required; time-of-day is discarded and SQL stores a DATE.
- Description is optional and limited to 500 characters.
- IsOptional distinguishes optional holidays from company holidays.
- One date can be registered only once per company. The database unique index
  is the final concurrency guard.
- Updates and deactivation require the current RowVersion.
- Stale updates are returned as HTTP 409 by the shared exception handler.
- Missing records return HTTP 404 for GET and PUT.
- Deactivation sets IsActive to false; it does not physically delete the row.
- All reads and writes are filtered by TenantId and CompanyId.

## Database

Migration `013_HolidayManagement.sql` creates `hrms.Holidays` and its
tenant-scoped stored procedures. Migration `014_HolidayPermissions.sql` adds
the four holiday permissions. Run the standard database migration command
before using these endpoints.
