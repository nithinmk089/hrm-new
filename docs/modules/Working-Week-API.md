# Working Week API

All routes require authentication and the matching permission.

## Company default

- `GET /api/v1/companies/{companyId}/working-week` — `working-week.read`
- `PUT /api/v1/companies/{companyId}/working-week` — `working-week.update`

## Office override

- `GET /api/v1/companies/{companyId}/offices/{officeId}/working-week` — `working-week.read`
- `PUT /api/v1/companies/{companyId}/offices/{officeId}/working-week` — `working-week.update`

## Request

Supply exactly seven entries. Day numbers follow ISO-8601 (Monday=1, Sunday=7).

```json
{
  "days": [
    { "dayOfWeek": 1, "isWorkingDay": true },
    { "dayOfWeek": 2, "isWorkingDay": true },
    { "dayOfWeek": 3, "isWorkingDay": true },
    { "dayOfWeek": 4, "isWorkingDay": true },
    { "dayOfWeek": 5, "isWorkingDay": true },
    { "dayOfWeek": 6, "isWorkingDay": true },
    { "dayOfWeek": 7, "isWorkingDay": false }
  ]
}
```

## Response behavior

The response includes `isConfigured`, `isInherited` and the effective
seven-day schedule. An office with no complete override inherits the company
default. If neither schedule is configured, `isConfigured` is false and
`days` is empty. Configure the company default before submitting leave
requests.

Saving replaces the complete schedule atomically. An office override can be
removed in a future API enhancement; until then, save the company default
schedule to reset its values, or retain the office override explicitly.

The database validates active company and office ownership using the
authenticated tenant and route identifiers. The client cannot supply TenantId.
