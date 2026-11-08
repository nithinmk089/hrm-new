# Timesheet Management V1 – Design

## 1. Scope

V1 provides weekly employee time capture, submission, manager approval/rejection, and employee/manager history. It is a time-recording and approval module, not a project-management, attendance, billing, or payroll module.

## 2. Period and calendar rules

- A timesheet represents one ISO-style Monday–Sunday week.
- WeekStartDate must be Monday; WeekEndDate is exactly six calendar days later.
- Work dates are SQL date values, not UTC timestamps.
- Entries may be recorded on any date in the week, including a configured non-working day. Working-week configuration is informational in V1; it does not prevent recording weekend or holiday work.
- Past and current weeks can be created. Future-week creation is rejected by the service/procedure.
- No submission cutoff or backdating window is enforced in V1.

## 3. Time entry rules

Each entry contains:
- WorkDate
- WorkCategory: ClientWork, Internal, Meeting, Training, Administration, Other
- Description (required, maximum 1,000 characters)
- Hours (greater than zero, maximum 24 per entry, in 0.25-hour increments)

The sum of entries for an employee and work date cannot exceed 24 hours. Weekly total is derived from entries and cannot exceed 168 hours. The database stores entry hours as decimal(5,2); it does not store a duplicated header total.

V1 does not require project/task codes, billable flags, cost centres, overtime classification, or attendance reconciliation.

## 4. Lifecycle

States:
- Draft: editable by the owning employee.
- Submitted: locked while awaiting manager decision.
- Approved: locked; terminal in V1.
- Rejected: editable by the employee and eligible for resubmission.

A week has at most one active timesheet per employee. A rejected timesheet is updated and resubmitted as the same timesheet, preserving its history. A new submission after rejection records Resubmitted.

On first creation, a Created history event is written. Submission, resubmission, approval, and rejection events are appended to TimesheetHistory.

## 5. Authorization and identity

- Resolve the employee from EmployeeUserMappings using the authenticated user and active tenant membership. Never match by email.
- TenantId and actor UserId come from validated claims, never request payloads.
- Employee operations are restricted to the mapped employee's own timesheets.
- Manager queue and decision operations are restricted to employees with a current EmployeeReportingAssignments row for the mapped manager employee.
- A manager cannot approve or reject their own timesheet.
- Permission codes are timesheets.read, timesheets.manage, timesheets.submit, timesheets.approve, and timesheets.reject.
- Every query and write is constrained by TenantId and CompanyId. Company access also passes the existing permission/scope authorization policy.

## 6. Approval rules

- Only Submitted timesheets can be decided.
- The employee's currently assigned direct manager at decision time must approve or reject.
- Rejection requires a non-empty comment; approval comment is optional.
- RowVersion is required for updates and decisions to prevent lost updates.
- Manager decision, timesheet state, and history event are committed in one SQL transaction.
- Approval does not mutate leave balances or attendance data.

## 7. API shape (planned)

Base route: /api/v1/companies/{companyId}/timesheets

Employee:
- GET /mine?weekStartDate=YYYY-MM-DD
- GET /mine/history
- POST / creates a draft for a week
- PUT /{timesheetId}/entries replaces the draft/rejected timesheet's entry set
- POST /{timesheetId}/submit

Manager:
- GET /pending-approvals
- POST /{timesheetId}/approve
- POST /{timesheetId}/reject

All operations derive the employee identity from the authenticated principal. Payloads must not contain TenantId, EmployeeId, Status, SubmittedAtUtc, reviewer identity, or calculated totals.

## 8. Data model

Migration 025 creates:
- hrms.Timesheets: weekly aggregate, lifecycle, reviewer and RowVersion.
- hrms.TimesheetEntries: dated categorized work lines.
- hrms.TimesheetHistory: append-only lifecycle history.

Migration 026 registers timesheet permissions.

## 9. Transaction and persistence requirements

Stored procedures will enforce ownership, tenant/company scope, week boundaries, current manager assignment, allowed transitions, rowversion, and daily/weekly hour limits. Entry replacement and submission must be transactional. Approval/rejection, state update and history insert must be atomic.

Use Dapper with stored procedures, async I/O and CancellationToken. SQL parameters must be strongly typed. No EF Core migrations.

## 10. Deferred

Project/task master integration, billing, cost rates, overtime rules, attendance integration, leave cross-checks, payroll export, configurable submission deadlines, multi-level approvals, delegation, attachments, and timesheet amendments after approval are deferred.
