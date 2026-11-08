# Leave Request Submission API

## Endpoint

`POST /api/v1/companies/{companyId}/leave-requests`

Permission: `leave-requests.create`

The authenticated user is mapped to an employee through the active
`hrms.EmployeeUserMappings` row. The request body does not accept EmployeeId,
TenantId, Status, DaysRequested, manager identity, or balance values.

## Request

```json
{
  "leaveTypeId": "leave-type-guid",
  "fromDate": "2026-11-09",
  "toDate": "2026-11-11",
  "isHalfDay": false,
  "halfDayPeriod": null,
  "reason": "Personal work"
}
```

Dates are calendar dates and use ISO `yyyy-MM-dd` representation.

## Submission behavior

- The API validates date order, maximum inclusive range, reason and half-day
  period.
- The authenticated employee, active leave type, configured working week,
  effective primary office assignments and active holidays are resolved from
  the database.
- Chargeable days are calculated by `LeaveDayCalculator`.
- A request with zero chargeable days is rejected.
- The request is checked against existing Pending and Approved date ranges.
- Each affected calendar-year balance must already exist and have sufficient
  available days.
- The request, approval-history entry, balance reservations/usages and balance
  ledger entries are committed in one SQL transaction.
- Leave types requiring approval are submitted as Pending and reserve days.
- Leave types not requiring approval are auto-approved and move days directly
  to UsedDays.
- A leave request crossing calendar years is split across each year's balance.
- All persistence procedures are tenant/company scoped and lock balance rows
  during reservation to prevent concurrent overbooking.

## Responses

- `201 Created`: request submitted.
- `400 Bad Request`: invalid input or invalid/unconfigured calendar.
- `401 Unauthorized`: no valid access token.
- `403 Forbidden`: permission or authenticated employee mapping missing.
- `404 Not Found`: leave type/employee not available in the company.
- `409 Conflict`: overlapping request, missing/insufficient balance, or
  manager assignment required for approval is unavailable.

Manager decisions, cancellation and request-history read endpoints are
implemented in subsequent workflow stages.
