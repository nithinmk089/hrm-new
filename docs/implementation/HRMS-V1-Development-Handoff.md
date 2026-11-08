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


UI-01 repository checkpoint:
- Latest feature branch commit at this checkpoint: `ebc51b1fa6c517081ff1cc5e81f8b95e5e3e225b`.
- Angular CLI development build is configured to replace the production environment with the local API URL. The API URL is centralized through the `API_BASE_URL` injection token.
- Registered Angular HttpClient for upcoming centralized API services. Added a Vitest smoke test for the application shell. It has been committed but has **not yet been executed**.


## 9. UI-02 progress checkpoint (2026-11-08)

Status: **In progress — first authentication vertical slice committed to `feature/ui-02-authentication`; local build and runtime verification are pending.**

- UI-01 was merged into `main` through PR [#4](https://github.com/nithinmk089/hrm-new/pull/4), merge commit `4e9983183a26d556dec79c591fa8bf80bf9e7803`.
- UI-02 branch: `feature/ui-02-authentication`, created from that exact latest-main merge commit.
- Added typed authentication contracts matching the backend `LoginRequest` and `LoginResult`.
- Added an in-memory Angular signal-based authentication session; access tokens are not written to localStorage/sessionStorage. Refresh tokens remain in the backend-managed HttpOnly cookie.
- Added login UI with tenant code, email and password, validation, loading and generic authentication error states.
- Added auth guard for all existing application routes, a bearer-token interceptor, and a layout-free login route.
- Login and refresh requests use `withCredentials: true`. Successful login/refresh also calls `GET /api/v1/auth/me` to load the current identity contract.
- The backend `/auth/me` contract currently returns userId, email, tenantId, tenantCode and membershipId. It does not return effective permissions. Do not invent client-side permissions or role assumptions; permission endpoint/navigation work remains pending backend contract review.
- Current UI-02 code has **not** been built or tested locally by the assistant. User verification is required before proceeding.

### UI-02 files added/updated in this slice

Added:
- `src/frontend/hrms-web/src/app/core/auth/auth.models.ts`
- `src/frontend/hrms-web/src/app/core/auth/auth.service.ts`
- `src/frontend/hrms-web/src/app/core/auth/auth.guard.ts`
- `src/frontend/hrms-web/src/app/core/auth/auth.interceptor.ts`
- `src/frontend/hrms-web/src/app/features/auth/login-page.component.ts`

Updated:
- `src/frontend/hrms-web/src/app/app.routes.ts`
- `src/frontend/hrms-web/src/app/app.config.ts`
- `src/frontend/hrms-web/src/app/app.component.ts`

### Important local development configuration

The backend's CORS policy and `HasTrustedOrigin()` check both use `Authentication:AllowedOrigins`. The Angular development server uses `http://localhost:4200`, while the API uses `http://localhost:5000`. Configure the API Development environment with the exact origin `http://localhost:4200` before testing cross-origin login. Do not weaken origin validation or enable wildcard origins with credentials.

### Next UI-02 actions

1. Fetch and pull `feature/ui-02-authentication` locally.
2. Run `npm run build` and `npm test` from `src/frontend/hrms-web`; share any compiler/test output before further changes.
3. Configure the development API allowed origin as described above, restart the API, and verify the login request/cookie behavior in the browser.
4. Complete 401 refresh/retry behavior, logout UI, current-user display, permission retrieval and permission-aware navigation only after confirming the backend permission model/endpoint.
5. Add focused auth service/interceptor/guard tests, then complete the UI-02 exit criteria and merge the branch into `main`.


### UI-02 runtime verification update (2026-11-08)

The user has now verified the following locally against the backend:
- Angular production build succeeded.
- Vitest suite passed: 1 test file, 1 test.
- Login succeeded against the API at `http://localhost:54480/api/v1`.
- Browser refresh restored the authenticated session using the HttpOnly refresh cookie.
- Logout worked; navigating directly to `/dashboard` after logout redirected to `/login`.
- A visible Sign out action was added to the application header (commit `9c94b0b051e89bfe794289fbdaf681008b7129f2`).
- Development API base URL was corrected to `http://localhost:54480/api/v1` to match `src/backend/HRMS.API/Properties/launchSettings.json` (commit `7e2ba9b31eaddfdb96985b0d741de5278699b050`).

### UI-02 security hardening commits

- `6affda10a5912733e3c56fb7a09bde6e3a389f56`: bearer-token interceptor is scoped to the configured HRMS API base URL; anonymous login/refresh/logout calls do not receive an Authorization header.
- `52802b77c833a9d1c243b263cdcb222e1e614f5d`: concurrent route-guard session restoration shares a single refresh request using RxJS shareReplay.

### Remaining UI-02 verification

- User still needs to verify invalid credentials produce the expected user-friendly 401 message.
- Re-run `npm install` only if dependencies changed; then run `npm run build` and `npm test` after the latest hardening commits.
- Add focused unit tests for authentication service/interceptor/guard behavior before final phase closure if practical.
- Do not claim these latest hardening changes have passed local build/tests until the user supplies the results.
- Once verification is complete, update this handoff and implementation plan, merge UI-02 into `main` through a PR, and create the next feature branch from the resulting latest `main` commit.


### UI-02 final verification (2026-11-08)

Status: **Completed and ready for integration.**

The user confirmed all final local checks are good after pulling the latest authentication hardening commits:
- Angular build succeeds.
- Frontend test suite succeeds.
- Login succeeds against local API `http://localhost:54480/api/v1`.
- Invalid credentials show the expected user-friendly 401 message while preserving the entered workspace code and email.
- Browser refresh restores the session using the backend HttpOnly refresh cookie.
- Logout revokes/clears the refresh session and clears the in-memory access-token session.
- Direct navigation to protected `/dashboard` after logout redirects to `/login`.
- Header exposes a working Sign out action.
- Access token interceptor is restricted to the configured HRMS API base URL.
- Concurrent session restoration is shared across simultaneous guard subscribers.

Key UI-02 commits include:
- `7e2ba9b31eaddfdb96985b0d741de5278699b050` — local API port 54480.
- `9c94b0b051e89bfe794289fbdaf681008b7129f2` — visible sign-out action.
- `6affda10a5912733e3c56fb7a09bde6e3a389f56` — API-scoped bearer interceptor.
- `52802b77c833a9d1c243b263cdcb222e1e614f5d` — shared concurrent session restoration.
- `128bb2e3e62faba897b6f1d8d93e6c8c0528330f` — interceptor regex syntax correction.
- `ce0c7f1a87474c929773910efb79d8e99fc661c0` — previous handoff checkpoint.

UI-02 does not yet include effective-permission retrieval or permission-aware menu/action visibility because the current `/auth/me` backend contract does not expose permissions and no verified effective-permissions endpoint has been integrated. Do not hardcode permissions. Treat that as a follow-up authorization slice after inspecting/adding the backend contract.

Next: open and merge the UI-02 PR into `main`. Only after merge, create the next UI feature branch from the resulting latest `main` commit. The planned next phase is UI-03 Dashboard.


### UI-02 integration completed (2026-11-08)

- Pull request [#5](https://github.com/nithinmk089/hrm-new/pull/5), **feat(frontend): complete UI-02 authentication experience**, was merged into `main`.
- Merge commit: `284acf88eddc64c186274712e43abda03d3cf519`.
- UI-02 was locally verified by the user: Angular build and tests passed; login, invalid-credential messaging, refresh/session restoration, logout, and protected-route redirection worked against the local API.
- The next branch, `feature/ui-03-dashboard`, was created directly from the exact UI-02 merge commit above, following the agreed branch workflow.

## 10. Current implementation checkpoint — UI-03 Dashboard

Active branch: `feature/ui-03-dashboard`.

Start UI-03 by inspecting existing dashboard component, shell, API contracts, and available dashboard-related backend endpoints. Do not invent metrics or display fabricated business data. First identify which real APIs/data can support the initial dashboard. If no dashboard aggregation endpoint exists, implement a useful navigation-first dashboard with accurate user/workspace context and clearly labeled empty states; propose a backend endpoint only when the required source data and authorization rules are confirmed.

After implementation, update this handoff, ask the user to run local build/tests, then merge UI-03 into `main` before creating another feature branch.


## 11. UI-03 Dashboard implementation checkpoint (2026-11-08)

Status: **Initial dashboard vertical slice committed; local verification pending.**

Backend/API inspection found no dedicated dashboard aggregation endpoint. Existing module APIs are permission-scoped and require company IDs and, for some employee-specific resources, employee IDs. Do not call these from the dashboard using guessed IDs or derive employee identity from email. No fabricated totals or pending counts are displayed.

Implemented on `feature/ui-03-dashboard`:
- Replaced generic dashboard welcome content with authenticated user display name from the in-memory AuthService session.
- Added current workspace name and tenant code from the authenticated login/refresh response.
- Added current date label and module quick-access links.
- Added a clear note explaining that employee totals, leave summaries and approval counts will be displayed only after a dedicated tenant-scoped dashboard data contract is available.
- Updated the global application header and sidebar tenant panel to show the authenticated display name and tenant name rather than hardcoded placeholder values.
- Added initials derived from the actual user/workspace labels for their avatars.

Commits:
- `87030653553fa181c3cb2c28ac9d9eaf23845282` — personalized dashboard workspace landing page.
- `3a5e3520dcca3c259e6b2c5409f1e97d973ca108` — authenticated identity in the global shell.
- `2aa6481a44e0ea711d0e753f8418e464b7658691` — this handoff checkpoint.

Next:
1. User pulls `feature/ui-03-dashboard` and runs frontend build/tests.
2. Visually verify dashboard on desktop and mobile; confirm display name and tenant match the authenticated account.
3. If the user wants real metrics in UI-03, first define a backend dashboard summary endpoint with tenant scoping and permission-safe aggregates; do not infer company/employee identifiers in the browser.
4. Once verified, merge UI-03 into `main`, then create the next feature branch from the exact latest main commit.


### UI-03 final verification (2026-11-08)

The user confirmed the latest Angular build and test suite both succeeded after the dashboard layout correction. The user also supplied screenshots confirming:
- Authenticated display name is shown in the dashboard welcome panel and application header.
- Authenticated tenant/workspace name and tenant code are displayed in the workspace panel and sidebar.
- Welcome panel and workspace panel are left-aligned with the intended horizontal layout.
- Quick-access cards and the dashboard information panel render correctly.
- No fabricated business metrics are shown.

UI-03 is complete and ready for PR integration. No dashboard summary endpoint was present in the inspected backend controllers. Existing module endpoints are permission-scoped and some require company/employee IDs; the dashboard deliberately does not guess these identifiers or query endpoints with fabricated context. Real metrics remain deferred pending a tenant-scoped, permission-safe dashboard API contract.

Next phase: UI-04 — Departments, Offices, and Designations on `feature/ui-04-organization-masters`. Existing backend controllers are present under `src/backend/HRMS.API/Modules/CompanyManagement/Controllers/`; inspect their request/response contracts and permission policies before wiring frontend CRUD.


### UI-03 integration completed (2026-11-08)

- Pull request [#6](https://github.com/nithinmk089/hrm-new/pull/6), **feat(frontend): deliver authenticated HRMS dashboard**, was merged into `main`.
- Merge commit: `816f9c1942e9ddc326f23fcb5ceee035adfbe0c5`.
- The user confirmed the Angular build and tests passed after the final dashboard layout fix and supplied screenshots verifying the rendered dashboard.
- The next branch, `feature/ui-04-organization-masters`, was created directly from the exact UI-03 merge commit above.

## 12. Current implementation checkpoint — UI-04 Organization Masters

Active branch: `feature/ui-04-organization-masters`.

Scope for this phase:
- Departments
- Offices
- Designations

Existing API controllers:
- `src/backend/HRMS.API/Modules/CompanyManagement/Controllers/DepartmentsController.cs`
- `src/backend/HRMS.API/Modules/CompanyManagement/Controllers/OfficesController.cs`
- `src/backend/HRMS.API/Modules/CompanyManagement/Controllers/DesignationsController.cs`

Before implementing UI/API integration, inspect the complete controller request/response DTOs, Application services, validation, list filtering/pagination behavior, permission policy names, and SQL persistence contracts. Preserve tenant scoping and rowversion/deactivation semantics. Never assume the current tenant is a company ID; inspect the company selection/membership relationship and how the authenticated user is authorized for a company. Implement one vertical slice at a time, beginning with Departments, then Offices, then Designations. Update this handoff as each slice is verified.

Local checkout:
```powershell
git switch main
git pull --ff-only origin main
git switch -c feature/ui-04-organization-masters origin/feature/ui-04-organization-masters
```


### UI-04A — Departments implementation checkpoint (2026-11-08)

Status: **First frontend vertical slice committed; local build/test and API verification pending.**

Backend contract inspected:
- Company list: `GET /api/v1/companies`, returns tenant-scoped CompanyDto values with `companyId`, `companyCode`, `displayName`, and `isActive`.
- Department list/detail/create/update/deactivate are under `/api/v1/companies/{companyId}/departments`.
- Policies: `departments.read`, `departments.create`, `departments.update`, `departments.deactivate`.
- Department create request: `departmentCode`, `departmentName`, `description`, `parentDepartmentId`.
- Department update request: `departmentName`, `description`, `parentDepartmentId`, `rowVersion`.
- Department DTO includes company ID, parent department ID, active flag, timestamps, and rowversion.
- Department code is immutable during update; code is normalized uppercase by the application service. Parent must be an active department in the same company; self-parenting is rejected.
- Deactivation requires the current 8-byte rowversion and returns conflict when concurrency/deactivation cannot be completed.

Implemented:
- `core/company/company.models.ts` — typed company and department contracts.
- `core/company/company-api.service.ts` — company list and department CRUD/deactivation HTTP methods.
- `features/company/departments-page.component.ts` — company selection, department list, active/inactive status, create/edit dialog, optional parent selection, deactivation confirmation, loading/empty/error states, and rowversion-based updates.
- `app.routes.ts` — dedicated lazy-loaded `/company/departments` route ahead of generic company placeholder route.

Company context:
- The authenticated session exposes Tenant ID but not Company ID. The UI therefore loads companies using the tenant-scoped companies endpoint and uses the selected returned Company ID for department calls. It never substitutes Tenant ID for Company ID.
- If exactly one active company is returned, it is selected automatically. With multiple companies, the user must select one. Only active companies are offered for maintenance.

Pending:
1. User pulls the branch and runs Angular build/tests.
2. Verify the Departments page loads companies and departments against the local API; verify create, edit, parent selection, and deactivate using an authorized account.
3. Resolve any compile/runtime/API contract issues before implementing Offices.
4. Continue with Offices and Designations only after Departments is locally verified.


### Department reactivation enhancement (2026-11-08)

- Added `PATCH /api/v1/companies/{companyId}/departments/{departmentId}/activate`.
- Reactivation requires the existing `departments.update` permission; this avoids introducing a new permission that would need role-grant/seed changes. It remains tenant/company scoped through the existing authorization handler and authenticated tenant claim.
- Request body reuses `DeactivateDepartmentRequest` with the current 8-byte `rowVersion`.
- Added `IDepartmentService.ActivateAsync`, repository contract/implementation and SQL procedure `hrms.usp_Departments_Activate`.
- Added migration `028_DepartmentActivation.sql`; existing migrations remain immutable.
- Department list now displays an Activate action for inactive records and calls the API with the latest rowversion. Activation refreshes the list and displays the success modal.
- Updated DepartmentService test fake for the new repository contract.
- Local backend build, migration, and frontend build/tests remain to be run by the user. Run migration 028 before testing the activation API.



### UI-04A Departments integrated (2026-11-08)

- Pull request [#7](https://github.com/nithinmk089/hrm-new/pull/7), **feat(organization): complete department management**, merged into `main`.
- Merge commit: `18067564f5c98462d1ed9745860f2860f1ef1f57`.
- User confirmed backend build, migration 028, frontend build/tests, and department create/edit/deactivate/reactivate verification succeeded.
- Next branch `feature/ui-04b-offices` was created directly from the merge commit above.

## 13. Current implementation checkpoint — UI-04B Offices

Active branch: `feature/ui-04b-offices`.

Inspected backend contract:
- Controller route: `/api/v1/companies/{companyId}/offices`.
- Policies: `offices.read`, `offices.create`, `offices.update`, `offices.deactivate`.
- Create request fields: `officeCode`, `officeName`, `addressLine1`, `addressLine2`, `city`, `stateOrProvince`, `postalCode`, `countryCode`, `timeZoneId`, `isHeadOffice`.
- Update request fields are the same except officeCode is immutable and RowVersion is required.
- DTO includes office/company IDs, code/name, address, country, time zone, head-office and active flags, timestamps and RowVersion.
- Service normalizes office code and country code to uppercase; country code must be two ASCII letters; required code/name limits 30/150; address line max 200; city/state max 100; postal code max 20; timezone ID max 100.
- Update/deactivation require an 8-byte RowVersion. Duplicate office codes are rejected within the company.
- Deactivation exists; no reactivation endpoint currently exists. Do not display an Activate action until a backend endpoint and persistence implementation are added, if the product requires reactivation.
- Company context must use the tenant-scoped company list endpoint and selected CompanyId, never TenantId.

Planned vertical slice:
1. Inspect repository SQL and stored procedure contracts and office-specific migrations before wiring UI.
2. Implement offices list with active/inactive status, head-office indicator, address and timezone details.
3. Implement create/edit modal with country/time-zone inputs and validation.
4. Implement styled deactivate confirmation and success/error dialogs, preserving RowVersion.
5. Add office reactivation only if supported through a deliberately added backend operation; confirm permission model and database constraints first.
6. User runs build/tests/migration and verifies list/create/edit/deactivate locally before proceeding to Designations.

Local checkout:
```powershell
git switch main
git pull --ff-only origin main
git switch -c feature/ui-04b-offices origin/feature/ui-04b-offices
```


### UI-04B Offices frontend implementation checkpoint (2026-11-08)

Status: **Frontend vertical slice committed; local build/test and API verification pending.**

Implemented:
- Typed `Office`, `CreateOfficeRequest`, and `UpdateOfficeRequest` contracts.
- `CompanyApiService` methods for list, create, update, and deactivate office endpoints.
- Lazy-loaded authenticated route `/company/offices`.
- Office directory with code/name, city/state, country, timezone, head-office indicator, active/inactive status, and action tooltips.
- Company selection uses the tenant-scoped `GET /api/v1/companies` response and the selected CompanyId; TenantId is never substituted for CompanyId.
- Create/edit modal covers office code/name, address, city, state, postal code, two-letter country code, time-zone ID and head-office flag.
- Office code is immutable during edit; request includes latest RowVersion.
- Deactivation confirmation and success dialogs match Departments UI. Inactive records remain visible as historical records; no Activate action is shown because the current backend has no office activation endpoint.
- Head-office uniqueness and duplicate office code errors are surfaced from API responses.

Backend contract and persistence inspected:
- Existing SQL procedures: `hrms.usp_Offices_GetAll`, `GetById`, `GetByCode`, `Create`, `Update`, `Deactivate`.
- Create validates active company ownership in the authenticated tenant.
- Only one active head office is enforced by database uniqueness constraints; update/create persistence translates duplicate key errors to user-facing validation errors.
- Deactivation clears `IsHeadOffice` and sets `IsActive = 0`.
- Update/deactivation use 8-byte RowVersion.
- No SQL migration is required for the current UI slice; migration 011 already contains the office stored procedures.

Local verification required:
1. Pull `feature/ui-04b-offices`.
2. Run `npm run build` and `npm test` in `src/frontend/hrms-web`.
3. Open Offices and verify company list and office directory.
4. Verify create with a unique code, edit fields, head-office selection, and deactivate.
5. Verify duplicate office code and second active head-office errors are displayed clearly.
6. Verify inactive offices remain visible and cannot be edited/deactivated again.
7. Do not proceed to Designations until the user confirms local verification.


### Frontend unit testing standard and Offices coverage (2026-11-08)

- UI unit tests are required for every feature page and shared service/interceptor/guard with meaningful behavior; do not treat a successful production build as sufficient UI verification.
- Use the existing Vitest + Angular TestBed setup. Keep tests deterministic by mocking API services with RxJS `of` / `throwError`; no live API or database dependency.
- Cover initial loading and empty/error states, company/tenant selection behavior, form validation, create/update payload mapping, RowVersion propagation, confirmation flows, success/error outcomes, and active/inactive actions.
- Added `offices-page.component.spec.ts` with nine test cases: initial company/office loading and selected company, no companies, company switching, required/two-letter country validation, normalized create payload, immutable office code and RowVersion on update, explicit deactivation confirmation, conflict handling, and inactive-office action visibility.
- Future UI pages should include their unit test file in the same feature branch before requesting merge. Shared components/services should receive focused tests where behavior warrants them.


### UI-04B Offices integrated (2026-11-08)

- Pull request [#8](https://github.com/nithinmk089/hrm-new/pull/8), **feat(organization): add Offices management UI**, merged into `main`.
- Merge commit: `727f1d93abcb0d54edf30e8e3aa67734c4904f64`.
- User confirmed the nine Offices Vitest/Angular TestBed cases and Angular production build passed, and verified the UI in the browser.
- Next branch `feature/ui-04c-designations` created directly from merge commit `727f1d93abcb0d54edf30e8e3aa67734c4904f64`.

## 14. Current implementation checkpoint — UI-04C Designations

Active branch: `feature/ui-04c-designations`.

Backend API contract verified:
- Base route: `/api/v1/companies/{companyId}/designations`.
- Permissions: `designations.read`, `designations.create`, `designations.update`, `designations.deactivate`.
- Create: `designationCode`, `designationName`, `description`, `jobLevel`.
- Update: `designationName`, `description`, `jobLevel`, `rowVersion`; code is immutable after creation.
- Deactivate: PATCH `/{designationId}/deactivate` with RowVersion.
- Code required, trimmed, uppercase, max 30; name required, max 150; description optional max 500; JobLevel optional positive small integer.
- Code uniqueness is company-scoped among non-deleted records; stale update/deactivation returns conflict.
- No activation endpoint exists; inactive records remain historical and do not show an Activate action.
- Existing SQL migration 012 provides stored procedures. No new database migration expected for this UI slice.

Implemented in current branch:
- Typed frontend Designation DTO/request contracts and CompanyApiService methods.
- Lazy authenticated route `/company/designations`.
- Company-scoped directory, active/inactive status, job level, create/edit/deactivate dialogs.
- Code normalization, immutable edit code, optional positive job level, RowVersion update/deactivation, error and confirmation states.
- Nine Vitest/Angular TestBed unit tests covering initial loading, no companies, company switch, validation, normalized create, update RowVersion, confirmation, conflict, and inactive action state.

Local verification pending:
- Pull branch `feature/ui-04c-designations`.
- Run `npm test` and `npm run build` in `src/frontend/hrms-web`.
- Verify list/create/edit/deactivate and duplicate code/invalid job-level scenarios in browser.
- Do not merge or start the next feature until user confirms.


### UI-04C Designations integrated (2026-11-08)

- Pull request [#9](https://github.com/nithinmk089/hrm-new/pull/9), **feat(organization): add Designations management UI**, merged into `main`.
- Merge commit: `047b744b6827e7a9cf9a60761e84e3574a954e39`.
- User confirmed the selected-company dropdown issue was fixed and Designations UI works as expected. Unit-test/build verification was completed.
- Next branch `feature/ui-05-holidays` created directly from the merge commit above.

## 15. Current implementation checkpoint — UI-05 Holidays

Active branch: `feature/ui-05-holidays`.

Verified backend contract:
- Base route: `/api/v1/companies/{companyId}/holidays`.
- Permissions: `holidays.read`, `holidays.create`, `holidays.update`, `holidays.deactivate`.
- Create: `holidayName`, `holidayDate`, `description`, `isOptional`.
- Update: same fields plus current 8-byte RowVersion encoded as Base64.
- Deactivate: PATCH `/{holidayId}/deactivate` with RowVersion.
- Holiday name required, trimmed, max 150; date required and normalized to date-only; description optional max 500; one holiday per company per date.
- Multi-day holidays are represented as one record per date. Recurrence, office-specific calendars and employee overrides are out of V1 scope.
- Inactive holidays remain historical; no activation endpoint exists.
- Existing migrations 013 and 014 provide schema/procedures and permissions; no new migration expected.

Design requirements:
- Company selection from tenant-scoped companies API; selected CompanyId only, never TenantId.
- Directory ordered by date with date, name, description, optional/mandatory indicator and active/inactive state.
- Create/edit modal with date, optional toggle and description; date-only payload should be sent as `YYYY-MM-DD` to avoid timezone shifts.
- Duplicate-date conflicts should be presented as a clear validation error.
- Deactivation uses confirmation modal and RowVersion.
- Add focused Angular TestBed/Vitest unit tests for loading, no company, selection, validation, date serialization, duplicate-date API error, update RowVersion, deactivate confirmation and inactive state.


Implemented on `feature/ui-05-holidays`:
- Typed Holiday contracts and CompanyApiService list/create/update/deactivate methods.
- Lazy authenticated route `/company/holidays`; existing sidebar Holidays link already targets this route.
- Company-scoped holiday directory ordered by date, with mandatory/optional and active/inactive indicators.
- Create/edit modal with date-only `YYYY-MM-DD` payload, optional flag and description.
- Duplicate-date validation, RowVersion update/deactivation, styled confirmation and success dialogs.
- Ten Angular TestBed/Vitest unit tests covering load, empty companies, company switch, validation, create/date payload, update RowVersion, duplicate date, deactivate confirmation, conflict and inactive actions.

Local build, unit tests, and browser verification pending. No migration required; migrations 013/014 are already part of the baseline.

### UI-05 Holidays integrated (2026-11-08)

- Pull request [#10](https://github.com/nithinmk089/hrm-new/pull/10), **feat(company): add Holidays management UI**, merged into `main`.
- Merge commit: `746471dee066d81c31fea8e25b3552e9980a66c7`.
- User-confirmed local verification: all 29 frontend tests passed (4 test files); Angular production build completed successfully.
- Fixed Bootstrap Icons stylesheet bundling by adding `node_modules/bootstrap-icons/font/bootstrap-icons.css` to Angular build styles and removing the invalid static stylesheet path from `index.html`.
- A non-blocking CommonJS warning remains for `bootstrap/dist/js/bootstrap.bundle.min.js`; build completes successfully.
- Development administrator initially received HTTP 403 for Holidays because the seeded SYSTEM_ADMIN role predated migration 014. Rerunning the repeatable development seed granted the newly active holiday permissions; user confirmed the Holidays page then worked.
- UI-05 implementation includes typed API contracts, company-scoped listing, create/edit/deactivate, duplicate-date messaging, RowVersion concurrency, and ten component tests.
- Next feature branch must be created from this merge commit on `main`.


## 16. Current implementation checkpoint — UI-06 Employee Management

Active branch: `feature/ui-06-employee-management`.

### UI-06A — Employee Core API foundation

Implemented on this branch:
- Added employee core application contracts and service under `src/backend/HRMS.Application/Employees/`.
- Added Dapper repository using stored procedures under `src/backend/HRMS.Infrastructure/Persistence/EmployeeRepository.cs`.
- Added authenticated controller at `/api/v1/companies/{companyId}/employees`.
- Added migration `029_EmployeeCoreManagement.sql` with `employees.read`, `employees.create`, and `employees.update` permissions and stored procedures for directory, detail, create, update and status changes.
- Registered employee repository/service in Infrastructure dependency injection.
- Directory supports optional search across employee code, names, preferred name and work email, and optional employment-status filtering.
- Create/update core profile fields: employee code (immutable after create), first/middle/last/preferred name, work email and hire date.
- Status changes support Preboarding, Active, OnLeave, Inactive and Terminated. Terminated requires a termination date; RowVersion is required for updates/status changes.
- Tenant ID is always taken from authenticated claims. Company ID is an explicit route scope and every SQL operation includes both TenantId and CompanyId.
- Employee assignment history, reporting-manager assignments, contacts, addresses, documents, pagination and frontend remain separate follow-up slices; no UI has been implemented yet.

API contract:
- `GET /api/v1/companies/{companyId}/employees?search=&status=`
- `GET /api/v1/companies/{companyId}/employees/{employeeId}`
- `POST /api/v1/companies/{companyId}/employees`
- `PUT /api/v1/companies/{companyId}/employees/{employeeId}`
- `PATCH /api/v1/companies/{companyId}/employees/{employeeId}/status`

Permissions:
- `employees.read`
- `employees.create`
- `employees.update`

Migration:
- Apply migration 029 explicitly with:
  `dotnet run --project .\\src\\backend\\HRMS.API\\HRMS.API.csproj -- --migrate`
- Re-run the repeatable development seed if the development administrator needs the newly introduced employee permissions:
  `dotnet run --project .\\src\\backend\\HRMS.API\\HRMS.API.csproj -- --seed-development`
  (only after confirming `DevelopmentSeed:Enabled=true` in local user-secrets).

Verification status:
- Repository changes are committed to this feature branch.
- Assistant has not run the .NET build, SQL migration or integration tests against the user's local SQL Server. Local verification is required before extending the API or merging.
- Next: run backend build and tests, apply migration 029, re-seed development permissions, then exercise the five endpoints in Swagger or an API client. Resolve any issues before beginning the employment-assignment slice.


## 17. UI-06 Employee Management integrated (2026-11-08)

Status: **Completed, locally verified by the user, and merged into `main`.**

- Pull request [#11](https://github.com/nithinmk089/hrm-new/pull/11), **feat(hrms): implement UI-06 employee management**, merged.
- Merge commit: `64e5009e25d0481463943782efee3bf2aec797fd`.
- Feature branch: `feature/ui-06-employee-management`.
- Backend migration: `029_EmployeeCoreManagement.sql`; the user confirmed migration and development seed succeeded.
- Backend verification supplied by user: solution build succeeded; `dotnet test .\\HRMS.sln` reported 116 tests, 0 failed, 0 skipped; employee list and complete CRUD/status API workflow verified.
- Frontend: employee directory, search/status filtering, create, detail, edit, immutable employee code, employment status changes, RowVersion concurrency handling, and pre-save confirmation dialogs for create/edit.
- Frontend test coverage: ten employee component tests; user confirmed final frontend test suite and production build succeeded after resolving the email validator and confirmation-dialog formatting issues.
- User confirmed browser workflows: create/edit confirmation dialog, cancel and confirm actions, employee status update, frontend tests, and production build.
- Latest feature branch head merged: `55197f3b04ef40d51d865e3fa0a1b9534dad1955`.
- Do not repeat migration 029 unless the local migration history indicates it has not been applied.

### Next phase — UI-07 Working Week and Employee User Mapping

- UI-07 branch must start from the latest `main` after PR #11 and the handoff/plan documentation update.
- Implement company working-week configuration, office-level working-week overrides, and explicit employee-to-user mapping administration.
- Preserve ISO weekday numbering (Monday=1 through Sunday=7), company defaults and office overrides.
- Employee login mapping must remain explicit and authorized; never infer it from matching email addresses.
- Inspect existing backend routes, DTOs, permissions, and stored procedures before implementing UI. Add backend contracts/migrations only if a required capability is genuinely missing.
- Add focused tests for load/save, company/office scope, validation, effective schedule display, mapping/unmapping authorization, and concurrency where supported.
- User local workflow remains: pull branch, run frontend tests/build, verify browser workflows, share results; only then merge the feature PR to `main`.
