# HRMS V1 Implementation Plan

## 1. Product scope

HRMS V1 is a modular-monolith HR management system. The first release includes:

- Employee Management
- Leave Management
- Timesheet Management
- Company Administration:
  - Departments
  - Offices
  - Designations
  - Holidays
  - Asset Types
  - Assets
  - Leave Types
  - Company Policies

The current backend implementation covers authentication/authorization, company and organization masters, leave foundations and workflows, and the Timesheet V1 backend slice. Asset Types, Assets, Company Policies, and remaining employee-management UI/API depth remain future work unless documented elsewhere in the repository.

## 2. Technology and architecture decisions

### Frontend
- Angular 21+.
- TypeScript strict mode.
- Standalone components and feature-oriented organization.
- Bootstrap 5.
- Restrained enterprise styling inspired by Metronic; professional, consistent, and not overly colorful.
- Reactive forms for data entry and validation.
- Centralized API clients/services.
- HTTP interceptors for authentication and consistent API error handling.
- Route guards and permission-aware navigation/actions.
- Reusable shared UI controls, validation presentation, loading/empty/error states, and confirmation patterns.

### Backend
- ASP.NET Core Web API on .NET 10.
- Modular monolith with Clean Architecture principles.
- Thin API controllers; use cases and business rules in Application/Domain layers.
- Infrastructure owns persistence and external integrations.
- Dapper and SQL Server stored procedures for data access.
- SQL scripts are the schema-change mechanism; do not introduce EF Core migrations.
- REST routes under `/api/v1`.
- Async I/O and CancellationToken propagation.
- Parameterized SQL only; no user-input SQL concatenation.
- Problem Details for API errors.

### Database and security
- SQL Server 2022+.
- Tenant-owned data carries TenantId and tenant scoping is enforced at every persistence boundary.
- Tenant, user, employee, role, and permission identity must be derived from validated server-side authentication context, never trusted from browser-supplied identifiers.
- Employee-to-login mapping is explicit; do not infer it by matching email addresses.
- Use RowVersion/optimistic concurrency where applicable.
- State transitions and related balance/history/ledger updates must be transactional.
- Do not weaken existing authorization or tenant-isolation checks to simplify UI integration.

### Quality
- Backend unit tests and integration tests.
- Frontend unit tests.
- Playwright end-to-end tests for key workflows.
- Build and test after each meaningful implementation slice.
- Manual API workflow verification may be deferred until UI integration, as agreed, but backend compilation and automated tests must continue to run during development.

## 3. Frontend implementation sequence

### UI-01 — Angular foundation and application shell
- Create the Angular application under `src/frontend/`.
- Configure strict TypeScript, routing, environments, and build scripts.
- Add Bootstrap 5 and establish global design tokens, typography, spacing, tables, forms, buttons, alerts, badges, and modal conventions.
- Implement responsive application shell: sidebar, top header, content area, breadcrumbs, and user menu.
- Create shared components for page headers, data tables, form fields, validation messages, loading, empty states, errors, confirmation dialogs, and pagination.
- Establish feature-based folder conventions and API/environment configuration.

**Exit criteria:** application builds; shell and navigation render responsively; shared styles/components are reusable.

### UI-02 — Authentication and authorization experience (completed 2026-11-08)
- Login screen and authentication state management.
- Integrate existing login, refresh, logout, and current-user APIs.
- Implement secure token/session handling consistent with the existing backend cookie and refresh-token design; do not expose refresh tokens to browser JavaScript.
- Add HTTP interceptor for access-token attachment and centralized 401/403/error handling.
- Add route guards and permission-aware menu/action visibility.
- Handle expired sessions and logout cleanly.

**Exit criteria:** login, refresh, current-user loading, protected routes, and logout work against the existing API. Completed and locally verified on 2026-11-08. Effective-permission retrieval and permission-aware navigation remain deferred until a verified backend permissions contract is available.

### UI-03 — Dashboard (completed 2026-11-08)
- Build an initial role-aware dashboard shell.
- Show only metrics and widgets backed by available APIs/data.
- Avoid inventing metrics or hard-coding fake production data.

**Exit criteria:** authenticated user lands on a useful dashboard with clear navigation. Completed and locally verified: authenticated user/workspace identity, responsive welcome and workspace panels, module shortcuts, and explicit no-fabricated-metrics state. No dashboard aggregation endpoint currently exists; real metrics remain deferred until a tenant-scoped, permission-safe API is designed.

### UI-04 — Company organization masters
Implement CRUD screens and API integration for:
- Departments
- Offices
- Designations

Include search/filter, active/inactive states, create/edit forms, validation, deactivation confirmation, and optimistic-concurrency error handling.

**Exit criteria:** organization master CRUD workflows work through the UI.

### UI-05 — Holidays and Leave Types
- Holiday list/create/edit/deactivate.
- Leave Type list/create/edit/deactivate.
- Date, entitlement, carry-forward, and other server-side validation feedback.

**Exit criteria:** company administrators can manage these masters through the UI.

### UI-06 — Employee Management
- Employee list, detail, create/edit, and employment/assignment views according to implemented API contracts.
- Integrate employee-to-user mapping as a separate explicit administrative workflow where authorized.
- Do not assume an employee login from matching email.

**Exit criteria:** employee lifecycle workflows are integrated with backend contracts.

### UI-07 — Working Week and Employee User Mapping
- Company working-week configuration.
- Office-level working-week overrides.
- Employee-to-user mapping administration.
- Clear explanation of company defaults and office overrides.

**Exit criteria:** schedule and mapping prerequisites for leave workflows can be configured through the UI.

### UI-08 — Leave Management
Employee experience:
- Leave balance summary.
- Apply for leave with leave type, dates, half-day period, and reason.
- Display chargeable/excluded dates and validation feedback where the API exposes them.
- View own requests and history.
- Cancel eligible requests.

Manager experience:
- Pending approval queue.
- Request detail and approval history.
- Approve/reject with required rejection comments.

Preserve server-side rules for working weeks, office overrides, holidays, chargeable days, annual balance reservations, cross-year splits, overlaps, manager authorization, and transactional balance updates.

**Exit criteria:** employee and manager leave workflows pass UI/API integration tests.

### UI-09 — Timesheet Management V1
Employee experience:
- Weekly Monday–Sunday timesheet view.
- Create or load a week.
- Add/edit/remove work entries.
- Work categories: ClientWork, Internal, Meeting, Training, Administration, Other.
- Display daily and weekly totals.
- Save draft, submit, and view history/status.

Manager experience:
- Pending approvals queue.
- Timesheet detail with entry breakdown and totals.
- Approve or reject; rejection requires comments.
- Show decision/history state.

Preserve backend rules: quarter-hour increments, per-entry max 24 hours, daily max 24 hours, weekly max 168 hours, future-week rejection, one active timesheet per employee/week, submitted lock, rejected edit/resubmit, RowVersion concurrency, and manager/reporting-line authorization.

**Exit criteria:** employee and manager timesheet workflows pass UI/API integration tests.

## 4. Suggested frontend structure

Adapt to the Angular CLI version and chosen application style, but retain clear feature boundaries:

```text
src/frontend/
  src/
    app/
      core/
        auth/
        guards/
        interceptors/
        layout/
        services/
      shared/
        components/
        forms/
        models/
        utils/
      features/
        dashboard/
        company/
          departments/
          offices/
          designations/
          holidays/
          leave-types/
          working-week/
        employees/
        leave/
          balances/
          requests/
          approvals/
        timesheets/
          employee/
          approvals/
      app.routes.ts
      app.config.ts
    environments/
    styles/
  angular.json
  package.json
```

Avoid creating speculative frontend abstractions before the first feature needs them. Keep API DTOs aligned with backend contracts and centralize environment-specific API base URLs.

## 5. Delivery workflow

For each UI phase:
1. Inspect existing backend contracts and repository conventions.
2. Agree the screen behavior and API mapping before coding.
3. Implement a small vertical slice.
4. Run Angular lint/build/unit tests and relevant backend tests.
5. Verify authorization, tenant context, validation, concurrency, and error states.
6. Update module documentation and the development handoff.
7. Commit the completed slice to a focused feature branch and merge through a reviewed PR.

## 6. Explicitly deferred from V1 unless separately approved

- Payroll processing and payroll export.
- Project/task-based timesheet billing.
- Overtime and attendance integration.
- Leave attachments, multi-level approvals, delegation, comp-off, and configurable accrual schedules.
- Automated leave accrual and year-end carry-forward jobs.
- Approved-timesheet amendment workflow.
- Advanced analytics not supported by implemented APIs.

## 7. Current task

UI-01 through UI-07 have been completed and integrated into `main`. UI-07 was merged through PR #12, merge commit `e03b84e42401706227d5b35727faa5d9655f7b5c`.

The current implementation task is **UI-08 — Leave Management** on `feature/ui-08-leave-management`. UI-08A (employee balance dashboard and leave submission), UI-08B (employee request history and cancellation), HR balance provisioning, and company Leave Type Administration are implemented; local verification is pending. Next implement UI-08C (manager pending approval queue) and UI-08D (request detail/history and approve/reject) against existing APIs. Preserve server-side calendar, balance reservation, tenant-scope, reporting-line and concurrency rules. HR leave balance provisioning is intentionally deferred to a later phase; do not add it to UI-08.


Leave balance provisioning policy enforcement: UI-08 now includes SQL migration `025_LeaveBalanceEntitlementLimits.sql`. Opening, Accrual and Adjustment transactions share the configured `AnnualEntitlementDays` cap (nullable means uncapped); CarryForward transactions are controlled separately by `AllowCarryForward` and `MaxCarryForwardDays`. Negative transactions are only allowed for Adjustment. Checks run inside the SQL transaction with locks on the leave type and balance rows, and the frontend provides early validation/hints. The SQL procedure remains authoritative. User must apply migration 025 before testing the new rules.
