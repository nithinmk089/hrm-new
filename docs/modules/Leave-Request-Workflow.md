# Leave Request Workflow

## Scope and implementation sequence

The leave-request workflow is implemented in incremental slices:

1. Chargeable-day calculation (implemented).
2. Tenant-scoped request submission, overlap protection and atomic balance reservation (implemented).
3. Manager approval/rejection (implemented).
4. Requester cancellation and balance restoration (implemented).
5. Employee request history (implemented); pagination and richer filtering can follow.

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
- Only the employee's currently assigned reporting manager can approve or reject; a manager cannot decide their own request.
- Decisions require the current RowVersion and update request, approval history, balance aggregates and ledger in one transaction.
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

Implemented in the manager approval slice:
- Manager pending-approval queue restricted to current direct reports.
- Approve and reject endpoints resolve the manager through the authenticated employee mapping.
- Decision-time manager assignment, pending status and RowVersion are rechecked in SQL.
- Approval converts reservations to used days; rejection releases reservations. Both write approval history and ledger entries atomically.

Implemented in the requester history and cancellation slice:
- Employee request list (latest 100) and action-history endpoints are restricted to the authenticated employee mapping.
- Pending cancellation releases reserved balance; approved cancellation restores used balance only before the leave start date.
- Cancellation requires the current RowVersion and writes the Cancelled history and negative ledger entry atomically with the request and balance updates.

Remaining enhancements:
- Pagination/filtering for employee history and manager queue.
- Company-local date policy for approved leave cancellation; current implementation compares the UTC calendar date.
