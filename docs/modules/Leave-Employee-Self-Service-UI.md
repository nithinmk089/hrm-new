# Leave Management — Employee Self-Service UI (UI-08A)

## Delivered scope

The employee leave dashboard at `/leave` provides:

- Company selection using the authenticated user's available companies.
- Secure resolution of the signed-in user's employee mapping for the selected company.
- Annual leave balances showing available, used and reserved days.
- An explicit empty state when an employee mapping or annual balance has not been provisioned.
- Leave application with leave type, date range, optional half-day AM/PM period and reason.
- Server-returned request status, annual allocation split and excluded dates after submission.

## API integration

- `GET /api/v1/companies/{companyId}/employee-user-mappings/mine`
  - Permission: `leave-balances.read`
  - Returns the employee ID for the authenticated user and requested company.
  - The server derives tenant and user from validated JWT claims.
  - Only active user, active tenant membership, active company, active employee and active mapping are accepted.
- `GET /api/v1/companies/{companyId}/leave-types`
  - Permission: `leave-types.read`
- `GET /api/v1/companies/{companyId}/employees/{employeeId}/leave-balances?year={year}`
  - Permission: `leave-balances.read`
- `POST /api/v1/companies/{companyId}/leave-requests`
  - Permission: `leave-requests.create`
  - Employee identity is resolved again by the backend; no employee ID is sent in the submission payload.

## Validation and business rules

- Dates are submitted as ISO calendar-date strings (`yyyy-MM-dd`), not UTC timestamps.
- End date cannot precede start date.
- Half-day leave must cover one date, use AM or PM, and be allowed by the selected leave type.
- Reason is required and limited to 1,000 characters.
- The browser does not calculate chargeable working days, holiday exclusions, office schedules, annual balance splits or overlap eligibility.
- The submission response is authoritative for requested days, annual reservations, excluded dates and status.
- Missing employee mapping or missing annual balance records are shown as actionable empty states; the UI does not fabricate a mapping or create a balance.

## Verification

Component tests cover company selection, authenticated employee resolution, balance display, missing mapping/balance states, date/half-day validation, normalized submission payload, server-returned excluded dates and conflict feedback.

Local .NET and Angular build/test verification and browser testing remain pending user execution.
