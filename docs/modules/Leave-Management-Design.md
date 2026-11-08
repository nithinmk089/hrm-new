# Leave Management V1 – Design

## Scope

This design builds on the existing tenant-scoped employee, reporting assignment,
office, holiday and leave-type records. It introduces employee identity mapping,
working-week configuration, leave balances, requests, approval history and an
immutable balance transaction ledger.

## Working-week policy

- Each company owns a seven-day default working-week schedule.
- An office may define a complete seven-day override.
- An office without an override inherits the company schedule.
- Day numbering is ISO-8601: Monday=1 through Sunday=7. This avoids dependence
  on SQL Server's session-level DATEFIRST setting.
- The employee's effective-dated primary employment assignment determines the
  office for each date in a leave range.
- A leave date is chargeable when it is a working day and is not an active,
  non-optional company holiday. Optional holidays remain chargeable if leave is
  requested on that date.
- A company schedule must be configured before leave can be submitted. An
  office override must contain exactly one row for each day of the week.

## Employee identity and approval

- Employee-to-user association is explicit and tenant-bound through
  EmployeeUserMappings. Email matching is not an identity mechanism.
- One active user mapping is allowed per employee and one active employee
  mapping per tenant user.
- Self-service operations derive the employee from the authenticated user
  mapping. EmployeeId in a request body is never trusted.
- The approver is the employee's effective-dated reporting manager for the
  request. Approval/rejection must verify that the authenticated actor maps to
  that manager and that the assignment is still valid.
- A manager cannot approve their own request.

## Leave calculation and validation

- Leave requests use calendar dates, not UTC timestamps.
- Start date must not be after end date; dates must be within an eligible
  employment period.
- Half-day leave is limited to one date and only leave types configured with
  AllowHalfDay=true. Half-day periods are AM or PM.
- Working days exclude weekly offs and active mandatory holidays. Optional
  holidays are not automatically excluded.
- Requests with zero chargeable days are rejected.
- Overlapping Pending or Approved requests for the same employee are rejected.
- Leave balances are maintained per employee, leave type and calendar year.
  A request crossing a calendar-year boundary is split across the relevant
  annual balances when submitted.
- A balance must exist for every affected leave year. V1 does not silently
  create or accrue balances.

## Balance lifecycle

- Opening, accrual, carry-forward and manual adjustment entries are recorded
  through balance transactions.
- Submission reserves the requested days, preventing concurrent requests from
  overspending the balance.
- Approval converts reserved days to used days.
- Rejection or cancellation releases reserved days.
- Cancellation of an approved request restores used days only if the leave has
  not started.
- Every balance mutation and request state transition occurs in one SQL
  transaction and writes a corresponding transaction/history row.
- Stored procedures use tenant/company predicates and row-level locking to
  serialize competing balance changes. RowVersion is used for administrative
  balance adjustments and other mutable configuration.

## Request lifecycle

- Leave types requiring approval enter Pending and are routed to the current
  reporting manager.
- Leave types not requiring approval are auto-approved during submission.
- Only the assigned manager can approve or reject a pending request.
- Only the requesting employee can cancel their own eligible request, subject
  to the cancellation rules above.
- Allowed transitions: Pending -> Approved, Pending -> Rejected,
  Pending -> Cancelled, Approved -> Cancelled (before the start date).
- Rejected and cancelled requests are terminal.

## V1 operational boundaries

- Balances are provisioned/adjusted by authorized HR users; automated monthly
  accrual and year-end carry-forward jobs are not part of this initial workflow.
- Payroll integration, attachments, multi-level approval chains, delegation,
  comp-off and configurable accrual schedules are deferred.
- All read and write APIs enforce authenticated tenant, company permission and
  employee/manager identity checks. TenantId, employee identity and approver
  identity are server-derived.
