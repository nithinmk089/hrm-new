# HRMS V1

An enterprise-oriented Human Resource Management System built as a modular monolith.

## V1 scope

- Employee management
- Leave management
- Timesheet management
- Company administration:
  - Departments
  - Offices
  - Designations
  - Holidays
  - Asset types and assets
  - Leave types
  - Company policies

The repository is being initialized in stages. The architecture is designed to support later HRMS capabilities without introducing microservices prematurely.

## Technology baseline

| Area | Standard |
|---|---|
| Frontend | Angular 21+, TypeScript strict mode |
| UI | Bootstrap 5, enterprise/Metronic-inspired design |
| Backend | ASP.NET Core Web API on .NET 10 |
| Database | Microsoft SQL Server 2022+ |
| Data access | Dapper and SQL Server stored procedures |
| Architecture | Modular monolith, Clean Architecture principles |
| API | REST, versioned under `/api/v1` |
| Testing | xUnit, integration tests, frontend unit tests, Playwright E2E |

**Database access decision:** V1 follows the established SQL standards: Dapper for application data access and stored procedures for database operations. EF Core migrations are not the default data-access approach. Schema changes must be versioned and reviewed as SQL scripts.

## Repository layout

```text
.
├── AGENTS.md
├── docs/
│   ├── architecture/
│   ├── database/
│   ├── development/
│   ├── security/
│   └── product/
├── src/
│   ├── backend/
│   └── frontend/
├── database/
│   ├── migrations/
│   ├── procedures/
│   ├── seed/
│   └── scripts/
├── tests/
└── deployment/
```

## Architecture principles

1. Start with a modular monolith; do not split into microservices without a measured need.
2. Enforce tenant isolation at every data-access boundary.
3. Keep business rules in the application/domain layers, not controllers or UI components.
4. Use explicit DTOs and validation; never expose persistence entities directly from APIs.
5. Use server-side pagination, filtering, sorting and authorization for data grids.
6. Record audit events for sensitive business changes.
7. Store timestamps in UTC and apply tenant/user time zones only for display and business-calendar calculations.
8. Treat employee and company data as sensitive; apply least privilege and secure defaults.

## Getting started

The source projects will be added in subsequent implementation steps. See [Architecture](docs/architecture/solution-overview.md), [V1 scope](docs/product/v1-scope.md), [Coding standards](docs/development/coding-standards.md), and [SQL standards](docs/database/sql-standards.md).

## Development workflow

- Use feature branches for changes.
- Keep pull requests focused and reviewable.
- Add or update tests with every behavior change.
- Add a database migration/script for every schema change.
- Never commit secrets, connection strings containing credentials, production data, or personal employee data.
