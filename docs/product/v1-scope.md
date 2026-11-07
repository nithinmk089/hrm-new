# HRMS V1 — Product Scope

## 1. Goal

Deliver a usable, secure foundation for essential HR operations. V1 prioritizes correct employee data, company configuration, leave workflows and timesheet workflows over broad feature count.

## 2. Included scope

### Company administration

| Capability | V1 responsibility |
|---|---|
| Departments | Create, view, update, deactivate; support department hierarchy only if required |
| Offices | Maintain office/location details and active status |
| Designations | Maintain job titles/designations |
| Holidays | Maintain tenant/office-specific holiday calendars and dates |
| Asset types | Maintain categories of company assets |
| Assets | Maintain asset inventory and assignment status/history at a basic level |
| Leave types | Configure leave categories and core entitlement/approval rules |
| Company policies | Publish and maintain policy metadata and documents/links, with effective dates and visibility |

### Employee management

- Employee profile and employee code.
- Personal and contact information, with appropriate access restrictions.
- Employment information: joining date, employment type/status, department, designation, office and manager.
- Reporting relationship and basic employee lifecycle status.
- Search, filters, sorting, paging and profile view/edit.
- Audit history for important changes.

### Leave management

- Leave application and employee self-service view.
- Configurable leave types and applicable holiday/weekend rules.
- Balance and entitlement tracking according to approved policy rules.
- Manager/authorized approver review, approval or rejection with comments.
- Cancellation and correction rules.
- Leave calendar and history.
- Concurrency-safe balance updates; prevent overlapping or invalid requests.

### Timesheet management

- Daily/weekly work-time entry.
- Draft, submit, approve, reject and resubmit workflow.
- Configurable period boundaries and submission rules.
- Manager/authorized approver review.
- Validation for duplicate entries, invalid durations and locked periods.
- Employee and manager timesheet views and basic summaries.

## 3. Cross-cutting requirements

- Tenant-aware data model and access checks.
- Authentication, role/permission authorization and employee data scopes.
- Audit trail for sensitive and business-critical changes.
- Notifications for key workflow events (in-app/email as configured).
- Responsive enterprise UI using Bootstrap 5.
- API documentation, health checks, structured logging and error handling.
- Automated tests for business rules, tenant isolation and approval workflows.

## 4. Explicitly out of scope for V1

Unless separately approved, do not include payroll, recruitment/ATS, performance management, LMS, travel, procurement, advanced analytics, biometric attendance, complex shift rostering, mobile native apps or microservice decomposition.

## 5. Key business rules to confirm during detailed design

1. Can an employee belong to more than one office or department at a time?
2. Are department/designation/office changes effective-dated?
3. Are leave entitlements annual, monthly accrual, anniversary-based or manually granted?
4. Which days count as leave: weekends, office holidays, half-days and intervening holidays?
5. Can leave requests span multiple leave types or split across balances?
6. Is approval single-level, multi-level or policy-dependent?
7. Can managers approve their own reports' leave when they are also delegates?
8. Are timesheets daily, weekly or both? What are minimum/maximum hours and rounding rules?
9. Can timesheets be edited after approval, and who can reopen a period?
10. Are assets assigned to employees, offices or both? What assignment/return history is required?
11. Are company policies files, rich text, links or a combination?
12. What employee data is visible to HR, managers, employees and administrators?

These decisions must be captured as requirements/ADRs before implementing dependent workflows.

## 6. Suggested delivery sequence

- Foundation: repository, solution, Angular shell, configuration, tenant/authentication design, audit and CI.
- Company setup: departments, offices, designations, holidays, leave types, asset types, assets and policies.
- Employee management: employee records, profile, search, manager relationships and lifecycle.
- Leave: policy calculations, balances, requests, approvals, calendar and notifications.
- Timesheets: entry, submission, approvals, period controls and summaries.
- Hardening: tenant isolation, accessibility, security review, performance, integration/E2E tests and deployment readiness.

## 7. Definition of done

A feature is done when its authorization and tenant scope are enforced server-side; validation and error states are implemented; audit and concurrency requirements are addressed; API/UI documentation is updated; unit and relevant integration tests pass; and the change has been reviewed.
