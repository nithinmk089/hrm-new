# Timesheet Management V1 API

Base route: /api/v1/companies/{companyId}/timesheets

All endpoints require authentication. TenantId and actor UserId are resolved from validated claims. The employee identity is resolved through the active EmployeeUserMappings record; no endpoint accepts EmployeeId or TenantId from the client.

## Employee endpoints

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | /mine?weekStartDate=YYYY-MM-DD | timesheets.read | Get own timesheet and entries for a Monday-start week |
| GET | /mine/history | timesheets.read | Get latest 100 own timesheets and lifecycle history |
| POST | / | timesheets.manage | Create a draft for a past/current week |
| PUT | /{timesheetId}/entries | timesheets.manage | Replace all entries on own Draft/Rejected timesheet |
| POST | /{timesheetId}/submit | timesheets.submit | Submit own Draft/Rejected timesheet |

### Create

Request:
```json
{
  "weekStartDate": "2026-11-02"
}
```

The week must start on Monday and cannot be in the future. Only one active timesheet is allowed per employee/week.

### Replace entries

Request:
```json
{
  "rowVersion": "BASE64_ROWVERSION",
  "entries": [
    {
      "workDate": "2026-11-02",
      "workCategory": "ClientWork",
      "description": "Implement employee timesheet API",
      "hours": 7.5
    },
    {
      "workDate": "2026-11-02",
      "workCategory": "Meeting",
      "description": "Daily project meeting",
      "hours": 0.5
    }
  ]
}
```

Entry replacement is all-or-nothing. Between 1 and 200 entries are required. Hours must be positive, no more than 24 per entry, and in 0.25-hour increments. Daily totals cannot exceed 24 and weekly totals cannot exceed 168. Every WorkDate must be within the timesheet week.

### Submit

Request:
```json
{
  "rowVersion": "BASE64_ROWVERSION"
}
```

Submission requires at least one entry and a current active reporting manager with an active user mapping. The timesheet becomes locked while submitted.

## Manager endpoints

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | /pending-approvals | timesheets.read | List submitted timesheets for current direct reports |
| GET | /{timesheetId} | timesheets.read | Get one submitted direct report timesheet and its entries |
| POST | /{timesheetId}/approve | timesheets.approve | Approve a submitted direct report timesheet |
| POST | /{timesheetId}/reject | timesheets.reject | Reject a submitted direct report timesheet |

Decision request:
```json
{
  "rowVersion": "BASE64_ROWVERSION",
  "comments": "Please clarify the time recorded for Tuesday."
}
```

Rejection requires comments; approval comments are optional. Only the employee's currently assigned direct manager may decide. A manager cannot decide their own timesheet.

## Lifecycle and concurrency

- Draft: employee can edit.
- Submitted: locked until manager decision.
- Approved: locked and terminal in V1.
- Rejected: employee can edit and resubmit the same timesheet.

Every state-changing operation requires the current 8-byte SQL RowVersion. Entry replacement, submission, approval/rejection, and corresponding history writes are transactional.

## SQL migrations

- 025_TimesheetManagementFoundation.sql
- 026_TimesheetPermissions.sql
- 027_TimesheetStoredProcedures.sql

Apply migrations from the repository root using the migration runner before starting the API.

## V1 limitations

No project/task master, billing, billable flag, overtime policy, attendance reconciliation, leave cross-check, payroll export, submission deadline, multi-level approval, delegation, or amendment after approval.
