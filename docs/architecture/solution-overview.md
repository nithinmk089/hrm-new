# HRMS V1 — Solution Architecture

## 1. Architectural decision

V1 is a **modular monolith** implemented with Clean Architecture principles. It is deployed as one backend application and one frontend application, with a single SQL Server database initially. Modules have explicit ownership and boundaries so they can evolve independently without the operational cost of microservices.

## 2. V1 business modules

| Module | Responsibilities |
|---|---|
| Company administration | Departments, offices, designations, holidays, asset types, assets, leave types and company policies |
| Employee | Employee profile, employment details, reporting relationships, status and lifecycle |
| Leave | Leave balance, leave application, approval/rejection, cancellation and holiday-aware calculations |
| Timesheet | Work entries, submission, approval/rejection, period locking and summaries |

Cross-cutting capabilities include identity/authentication, authorization, tenant context, audit, notifications, configuration, file metadata and observability. Implement only the capabilities needed to support the V1 workflows.

## 3. Logical architecture

```text
Angular Web Application
        |
        | HTTPS / JSON (/api/v1)
        v
ASP.NET Core API
  - Authentication and authorization
  - Tenant context resolution
  - Request validation
  - API contracts / Problem Details
        |
        v
Application Layer
  - Use cases / handlers
  - Validation and policies
  - Interfaces for persistence and external services
        |
        +--------------------+
        |                    |
        v                    v
Domain Layer             Infrastructure
  - Entities                - Dapper repositories
  - Value objects           - SQL connection factory
  - Domain rules             - Email / file / external adapters
  - Domain events            - Logging / telemetry adapters
        |                    |
        +---------+----------+
                  v
             SQL Server
        - Schemas / tables
        - Stored procedures
        - Views where justified
        - Versioned SQL scripts
```

Dependency direction: API and Infrastructure depend inward on Application/Domain. Domain must not depend on Application, Infrastructure, API, Dapper or ASP.NET Core.

## 4. Proposed backend solution structure

The exact project split should remain lean. Use these projects unless implementation evidence justifies a change:

```text
src/backend/
├── HRMS.sln
├── HRMS.Domain/
│   ├── Common/
│   ├── Company/
│   ├── Employees/
│   ├── Leave/
│   └── Timesheets/
├── HRMS.Application/
│   ├── Abstractions/
│   ├── Common/
│   ├── Company/
│   ├── Employees/
│   ├── Leave/
│   └── Timesheets/
├── HRMS.Infrastructure/
│   ├── Persistence/
│   ├── Identity/
│   ├── Notifications/
│   ├── Files/
│   └── Observability/
└── HRMS.API/
    ├── Endpoints/
    ├── Middleware/
    ├── Authentication/
    └── Configuration/
```

Do not create separate Shared, Persistence, Workers or Contracts projects merely for naming symmetry. Add a project only when it has a distinct responsibility and dependency boundary. A background worker can be introduced when a concrete asynchronous workload requires it.

## 5. Proposed frontend structure

```text
src/frontend/hrms-web/
├── src/app/
│   ├── core/                 # auth, tenant context, interceptors, guards
│   ├── layout/               # shell, navigation, header, page layout
│   ├── shared/               # reusable controls, pipes, directives
│   ├── features/
│   │   ├── company/
│   │   ├── employees/
│   │   ├── leave/
│   │   └── timesheets/
│   └── app.routes.ts
├── src/assets/
└── ...
```

Use standalone components, lazy-loaded feature routes and typed API clients/services. Keep feature-specific components and services inside their feature folder.

## 6. Tenant model

Design for multiple companies/tenants from the first schema. Every tenant-owned record is scoped by TenantID. Resolve tenant context from a trusted authenticated identity or validated host-to-tenant mapping; never trust a tenant identifier supplied only by the client.

For V1, a shared database with shared tables and TenantID is the default. Enforce tenant scoping in repository methods and stored procedures, and test cross-tenant access denial. A future database-per-tenant deployment should not require rewriting domain rules.

## 7. API conventions

- Base path: `/api/v1`.
- Resource-oriented routes and HTTP methods.
- Use DTOs, explicit validation and consistent Problem Details errors.
- Use UTC timestamps in transport contracts (ISO 8601).
- Use server-side pagination, filtering and sorting for collections.
- Return stable identifiers; do not expose internal database implementation details.
- Generate and publish OpenAPI documentation.
- Propagate a correlation/trace identifier through logs and responses.

## 8. Security baseline

- Authenticate users before accessing protected resources.
- Enforce role/permission authorization on the server.
- Apply tenant and employee data-scope checks to every use case.
- Store password hashes using a modern, vetted password-hashing implementation; never store plaintext passwords.
- Use short-lived access tokens and securely managed refresh-token rotation/revocation if JWT authentication is selected.
- Prefer secure, HttpOnly, SameSite cookies for browser refresh-token storage; assess CSRF protection and deployment topology.
- Audit sensitive changes and access to sensitive employee records where required.
- Keep secrets in environment-specific secret stores, not source control.

## 9. Operational baseline

- Environments: Local, Dev, QA, UAT and Production; add further stages only when delivery needs them.
- Structured logs, health checks and application metrics.
- Centralized exception handling and safe error responses.
- Configuration through validated options and environment variables/secret stores.
- SQL scripts are versioned and applied in a controlled deployment step.
- CI validates formatting, builds, tests and security-sensitive configuration.

## 10. Architecture decision records

Record material decisions under `docs/architecture/decisions/` using a short ADR format: context, decision, alternatives, consequences and status. Decisions requiring explicit confirmation before implementation include authentication/token delivery, tenant onboarding, employee-number generation, file storage and approval delegation.
