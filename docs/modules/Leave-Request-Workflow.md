# Leave Request Workflow

## Scope and implementation sequence

The leave-request workflow is implemented in incremental slices:

1. Chargeable-day calculation (implemented).
2. Tenant-scoped request submission, overlap protection and atomic balance reservation (implemented).
3. Manager approval/rejection.
4. Requester cancellation and balance restoration.
5. Request history and approval queue.

This document is the functional baseline for the remaining slices.

## Chargeable-day rules

The Application layer exposes a pure calculator in
`HRMS.Application.LeaveRequests.LeaveDayCalculator`. It has no database,
HTTP, or Infrastructure dependency.

- Date ranges are inclusive and use `DateOnly`; no UTC conversion is applied.
- ISO weekdays are Monday=1 through Sunday=7.
- A company working week must contain exactly one entry for all seven days.
- An effective-dated primary office assignment selects the office schedule for
  each date. If no office assignment is effective, the company schedule applies.
- A complete office schedule overrides the company schedule for its office.
- Active mandatory holidays are excluded, even when the weekday is normally
  working.
- Active optional holidays are chargeable when requested, including when the
  date would otherwise be a weekly off.
- Inactive holidays do not affect the calculation.
- A half-day request must cover exactly one date and the selected leave type
  must allow half days. A chargeable half-day is 0.5 days.
- The calculator rejects invalid or overlapping primary-office assignment
  ranges and rejects request ranges longer than 366 calendar days.
- The caller must reject a request whose calculated chargeable days are zero.

The calculator returns the total, the exact chargeable dates, and the excluded
dates. This makes the calculation testable and allows the eventual request API
to explain how the total was derived.

## Remaining workflow invariants

- The authenticated user is mapped to an employee through
  `hrms.EmployeeUserMappings`; do not accept the requester employee ID from
  the browser.
- A leave request must be scoped by the authenticated tenant and route company.
- Pending and Approved requests block overlapping date ranges for the same
  employee.
- Every affected leave-year balance must already exist before submission.
- Submission reserves balance days in the same SQL transaction as inserting
  the request and Submitted history record.
- Approval moves reserved days to used days; rejection releases reserved days.
- A leave type that does not require approval is auto-approved at submission.
- The reporting manager is resolved from the effective-dated
  `hrms.EmployeeReportingAssignments` table, not from request input.
- Managers cannot approve their own requests.
- Pending cancellation releases reservations. Approved cancellation is allowed
  only before the leave start date and restores used balance.
- Request transitions, approval history, balance aggregates, and ledger
  transactions must commit or roll back together.
- All SQL operations must include authenticated TenantId and CompanyId
  predicates. RowVersion protects request transitions against stale updates.


## Submission API status

Implemented:
- `POST /api/v1/companies/{companyId}/leave-requests`.
- Requester identity is resolved from the authenticated user mapping.
- Active leave type, company calendar, office assignments and holiday data are
  loaded through a tenant/company-scoped stored procedure.
- Chargeable dates are split into calendar-year allocations.
- Pending requests reserve days; leave types not requiring approval are
  auto-approved and move the days directly to UsedDays.
- SQL migration 022 adds the context and submission procedures. The request,
  Submitted/automatic-approval history, balance aggregates and ledger entries
  are committed in one transaction.
- Missing manager assignment, overlapping dates, missing annual balance or
  insufficient balance prevents submission.

Not yet implemented:
- Manager approval/rejection endpoints and authorization against the manager's
  current employee mapping.
- Employee request list/detail endpoints and manager approval queue.
- Cancellation and balance restoration.
