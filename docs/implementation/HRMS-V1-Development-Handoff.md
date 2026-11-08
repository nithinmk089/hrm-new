# HRMS V1 Development Handoff

> Purpose: this document is the durable resume point for a new development session. Read it together with `HRMS-V1-Implementation-Plan.md`, the architecture documents, and the module-specific API/design documents.

## 1. Repository and current baseline

- Repository: [nithinmk089/hrm-new](https://github.com/nithinmk089/hrm-new)
- Main branch is the integration branch.
- Current work was developed on `feature/timesheet-management-v1`.
- Feature branch head before this handoff documentation: `f5bca9863f9c9e90b8fef1c41f8629b899656934` (fix timesheet permission identity insert).
- The feature branch was 41 commits ahead of main and 0 behind immediately before the handoff documentation commit.
- The current Timesheet branch includes the earlier authentication, organization, employee/leave foundation and workflow work in its history.
- PR #1 previously merged authentication/authorization foundation into main (squash commit `795e10abd0176a92a36ac0b4fe48d4af5cb60c4a`). Do not attempt to merge PR #1 again.
- A new PR should be created from `feature/timesheet-management-v1` to `main` for the current integration.

## 2. Confirmed local verification

The user ran the following migration command locally from the repository root:

```powershell
dotnet run --project .\src\backend\HRMS.API\HRMS.API.csproj -- --migrate
```

The user supplied output confirming all three Timesheet migrations were applied:
- `025_TimesheetManagementFoundation`
- `026_TimesheetPermissions`
- `027_TimesheetStoredProcedures`

The user also explicitly confirmed that all of these succeeded:
- `dotnet restore`
- `dotnet build`
- `dotnet test`

Do not claim that manual Timesheet API workflows have been tested. The agreed plan is to defer manual endpoint verification until the UI is implemented, then test end-to-end through the UI. Automated backend tests and builds remain required during implementation.

## 3. Backend implementation already in repository

### Authentication and authorization
- Password hashing, JWT login, refresh-token rotation, logout.
- Refresh token/session cookie and relevant origin/CSRF protections.
- Tenant membership and permission/scope-aware authorization.
- Company CRUD APIs and development bootstrap seed.
- Swagger JWT Authorize configuration.
- API error logging middleware; errors other than 401/404 are intended to be recorded in `audit.ErrorLogs`.
- Existing seed password was exposed in an earlier conversation. Do not repeat it; rotate it if it was reused outside this local development environment.

### Company organization masters
- Departments: CRUD/list/detail/deactivate, parent validation, tenant/company scope, RowVersion concurrency.
- Offices: CRUD/list/detail/deactivate, country-code and field validation, head-office rules, RowVersion concurrency.
- Designations: CRUD/list/detail/deactivate, optional positive job level, RowVersion concurrency.
- Holidays: company-level holiday CRUD/deactivation, one record per company/date, optional holiday flag.
- Leave Types: CRUD/deactivation, annual entitlement and carry-forward validation.
- Permissions and stored procedures were added through SQL migrations.
- API controllers are grouped under module-specific controller folders.

### Leave foundation and workflow
- Company-level default working week; office-level overrides; ISO weekday numbering Monday=1 through Sunday=7.
- Explicit EmployeeUserMappings; no email matching.
- Employee leave balances with ledger transactions.
- Leave day calculation considers effective primary office assignment, office schedule override, weekly offs, mandatory holidays, optional holidays, and half-days.
- Submission maps authenticated user to employee server-side, validates chargeable days and overlapping requests, splits cross-year requests, reserves balance, and writes history/ledger transactionally.
- Manager approval/rejection is restricted to current direct reporting line; manager cannot decide own request. Approval converts reserved days to used days; rejection releases reservation.
- Employee can view own requests/history and cancel eligible pending requests; approved cancellation is allowed only before start and restores used days.
- SQL migrations 017–024 establish this foundation and workflow.

### Timesheet Management V1
Design is documented in `docs/modules/Timesheet-Management-Design.md`; API routes and contracts in `docs/modules/Timesheet-Management-API.md`.

Core rules:
- Weekly Monday–Sunday period; SQL `date`/calendar dates, not UTC timestamps for WorkDate.
- Past/current weeks may be created; future week is rejected.
- Entries may be on any date including weekends/holidays.
- Work category: ClientWork, Internal, Meeting, Training, Administration, Other.
- Description max 1000 characters.
- Hours must be greater than zero, no more than 24 per entry, and in quarter-hour increments.
- Daily total max 24 hours; weekly total max 168 hours.
- Statuses: Draft, Submitted, Approved, Rejected.
- One active timesheet per employee/week.
- Draft editable; Submitted locked; Approved terminal; Rejected editable and resubmittable.
- Employee identity comes from active EmployeeUserMappings and validated tenant membership.
- Manager queue and decisions are restricted to current direct reports; manager cannot approve/reject own timesheet.
- Rejection comments required; approval comments optional.
- RowVersion protects updates; decision, status, and history are transactional.
- V1 excludes project/task billing, overtime, attendance integration, leave cross-check, payroll export, deadlines, multi-level approval, delegation, attachments, and approved amendment.

Timesheet schema and APIs:
- Migration 025: `hrms.Timesheets`, `hrms.TimesheetEntries`, `hrms.TimesheetHistory`.
- Migration 026: timesheets.read/manage/submit/approve/reject permissions.
- Migration 027: stored procedures for mine/history/create/save/submit/pending approvals/decision/manager detail.
- Application DTOs, repository/service abstractions and service.
- Infrastructure Dapper repository and DI registration.
- Employee and manager API controllers.
- Unit tests and module docs.

### Recent migration correction
The first local run of migration 026 failed because the script tried to insert a GUID into an integer identity PermissionId. The migration was corrected to insert only the permission business columns and allow SQL Server to generate PermissionId. Commit: `f5bca9863f9c9e90b8fef1c41f8629b899656934`. The user then reran migrations and confirmed 025, 026, and 027 all applied successfully.

## 4. Important known limitations / review points

These are known design/code review points, not claims that a production issue has been observed:
- Some leave/timesheet date decisions currently use UTC date rather than a company-local date policy. Revisit before production.
- Leave request chargeable-day calculation context is read before the submission transaction; schedule/holiday/assignment changes could theoretically occur before commit. Consider a calendar/configuration version check or revalidation inside the transaction.
- Leave approval queue and manager decisions depend on current reporting assignment; integration tests should validate edge cases.
- Approved leave cancellation currently compares against UTC date, not company-local date.
- Timesheet pending-approval SQL should be reviewed for duplicate current reporting-assignment rows; an EXISTS predicate is safer than a join if duplicate assignments can occur.
- Confirm `TimeProvider` is registered in DI if the service constructor requires it (e.g. `services.AddSingleton(TimeProvider.System)`).
- Add SQL Server integration tests for stored procedures, tenant isolation, RowVersion conflicts, transaction atomicity, and manager authorization. Current unit tests do not replace persistence/integration coverage.
- Manual Timesheet APIs have not yet been tested by the user; defer that manual exercise until UI implementation is integrated, per agreement.

## 5. Frontend plan

The frontend has not yet been implemented. Follow `docs/implementation/HRMS-V1-Implementation-Plan.md`.

Agreed sequence:
1. UI-01 Angular foundation, theme, shell, sidebar, header, routing, shared components.
2. UI-02 Login, authentication/session, interceptors, route guards, permissions.
3. UI-03 Dashboard.
4. UI-04 Departments, Offices, Designations.
5. UI-05 Holidays and Leave Types.
6. UI-06 Employee Management.
7. UI-07 Working Week and Employee User Mapping.
8. UI-08 Leave employee and manager workflows.
9. UI-09 Timesheet employee and manager workflows.

Frontend stack: Angular 21+, strict TypeScript, standalone components, Bootstrap 5, restrained Metronic-inspired enterprise UI, reactive forms, centralized API services, JWT/session-aware interceptor, permission-based navigation.

## 6. Exact next-session instructions

1. Read this handoff and `docs/implementation/HRMS-V1-Implementation-Plan.md`.
2. Confirm current `main` includes the Timesheet/backend branch merge.
3. Ask the user to switch/pull the updated main branch locally if required:
   ```powershell
   git switch main
   git pull --rebase origin main
   ```
4. Start UI-01 only. Inspect the repository for existing frontend files before scaffolding; do not overwrite unrelated files.
5. Create a dedicated frontend feature branch from the latest main.
6. Implement the Angular application shell and verify build/tests.
7. Keep this handoff updated after each meaningful phase, including commit/PR references, files, migrations, tests, and known issues.

## 7. User working environment

- Local repository path: `D:\Nithin\Projects\My Company Projects\HR Management\hrm-new`
- Shell: PowerShell on Windows.
- Backend API has previously run on `http://localhost:5000`; use current launch settings as source of truth.
- SQL Server database is local development database configured by the repository's appsettings/user secrets. Never put connection strings or credentials in this document.
