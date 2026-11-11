# Employee-to-user mapping API

Employee identity must be mapped explicitly. Work email addresses are not used
to infer authentication identity.

Base route: `/api/v1/companies/{companyId}/employee-user-mappings`

All endpoints require authentication and tenant/company permission.

- `GET /by-employee/{employeeId}` — `employee-mappings.read`
- `GET /mine` — `leave-balances.read`; resolves the signed-in user's active employee mapping for the requested company. Returns only `companyId` and `employeeId`; returns `404` when no active mapping exists.
- `GET /available-users?search={term}` — `employee-mappings.read`; lists enabled users with active membership in the authenticated tenant who do not already have an active employee mapping.
- `POST /` — `employee-mappings.manage`
- `PATCH /{mappingId}/deactivate` — `employee-mappings.manage`

Create request:

```json
{
  "employeeId": "employee-guid",
  "userId": "user-guid"
}
```

Deactivate request:

```json
{
  "rowVersion": "base64-rowversion"
}
```

The authenticated tenant is read from the JWT. The stored procedure verifies
that the employee is active in the requested company and that the user has an
active membership in the same tenant. Database unique indexes prevent an
employee or tenant user from having more than one active mapping.

The API is intended for authorized HR administrators. Self-service employee
and manager actions must resolve the employee through this mapping server-side;
they must never accept an employee or approver identity from the request body.
