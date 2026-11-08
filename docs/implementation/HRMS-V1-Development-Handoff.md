# HRMS V1 Development Handoff

> Purpose: this document is the durable resume point for a new development session. Read it together with `HRMS-V1-Implementation-Plan.md`, the architecture documents, and the module-specific API/design documents.

## 1. Repository and current baseline

- Repository: [nithinmk089/hrm-new](https://github.com/nithinmk089/hrm-new)
- Main branch is the integration branch.
- The backend and Timesheet V1 work was developed on `feature/timesheet-management-v1` and integrated into `main` through PR [#3](https://github.com/nithinmk089/hrm-new/pull/3).
- PR #3 title: **Integrate HRMS V1 backend and Timesheet Management**; status: merged.
- PR #3 squash merge commit / current `main` head at integration: `17d5697fa2f2feb44fbdd2b6df58de01daf2f57f`.
- PR #3 included 43 commits, 18 changed files in the final PR diff, plus the prior branch history already present in the source branch.
- PR #1 previously merged authentication/authorization foundation into main (squash commit `795e10abd0176a92a36ac0b4fe48d4af5cb60c4a`). It is already integrated; do not attempt to merge PR #1 again.
- `main` is now the authoritative shared baseline. Start the next frontend feature branch from the latest `main`.

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

1. Read this handoff and `docs/implementation/HRMS-V1-Implementation-Plan.md` from the latest `main`.
2. In the user's local PowerShell terminal, sync the merged main branch:
   ```powershell
   git switch main
   git pull --rebase origin main
   ```
   If Git reports local uncommitted changes, inspect and preserve them; do not reset or discard them.
3. Start UI-01 only. Inspect the repository for existing frontend files before scaffolding; do not overwrite unrelated files.
4. Create a dedicated frontend feature branch from the latest main, for example `feature/angular-application-shell`.
5. Implement the Angular application shell and verify build/tests.
6. Keep this handoff updated after each meaningful phase, including commit/PR references, files, migrations, tests, and known issues.

## 7. User working environment

- Local repository path: `D:\Nithin\Projects\My Company Projects\HR Management\hrm-new`
- Shell: PowerShell on Windows.
- Backend API has previously run on `http://localhost:5000`; use current launch settings as source of truth.
- SQL Server database is local development database configured by the repository's appsettings/user secrets. Never put connection strings or credentials in this document.


## 8. UI-01 progress (2026-11-08)

Status: **In progress — initial Angular workspace and responsive shell committed to the feature branch; local build/test verification is still pending.**

- Feature branch: `feature/angular-application-shell`, created from the current `main` head `3382d1a028e1e9f4b4a9dfccb742f2ba63770432`.
- Confirmed that `src/frontend/` did not exist on `main`; no existing frontend files were overwritten.
- Angular workspace: `src/frontend/hrms-web/`, Angular 21 standalone application, strict TypeScript, Bootstrap 5 and Bootstrap Icons.
- Added responsive dark enterprise sidebar, top header, mobile navigation drawer/backdrop, workspace navigation, dashboard landing page and explicit placeholders for the not-yet-implemented V1 feature routes.
- Added shared global design tokens/styles, API base URL injection token, production and development environment files (development API base URL defaults to `http://localhost:5000/api/v1`), and an application-shell smoke test.
- Dashboard intentionally displays no fabricated business metrics. Authentication/session handling remains UI-02; placeholder routes are not functional module screens.
- Files are under `src/frontend/hrms-web/`; the workspace README documents local setup.
- No local Node/npm execution environment was used in this repository operation. **Do not mark build or tests as passing yet.**

### Next UI-01 actions

1. In PowerShell, run `git fetch origin`, `git switch feature/angular-application-shell`, and `git pull --rebase origin feature/angular-application-shell` (preserve any local work).
2. From `src/frontend/hrms-web`, run `npm install`, `npm run build`, and `npm test`; resolve any Angular CLI/compiler/test-runner issues.
3. Review responsive behavior at desktop/tablet/mobile widths and keyboard navigation/focus visibility.
4. Update this handoff with actual build/test results, then open a PR into `main`. Do not start UI-02 until UI-01 exit criteria are met.
