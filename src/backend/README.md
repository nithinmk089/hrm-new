# Backend solution

The backend is a modular monolith with four projects:

- **HRMS.Domain** — domain entities, value objects, domain rules and events. No infrastructure dependencies.
- **HRMS.Application** — use cases, validation, application contracts and persistence abstractions. Depends on Domain.
- **HRMS.Infrastructure** — Dapper, SQL Server connection and stored-procedure adapters. Depends on Application and Domain.
- **HRMS.API** — ASP.NET Core HTTP host, composition root, middleware and endpoint mapping. Depends on Application and Infrastructure.

## Dependency direction

```text
HRMS.API ------> HRMS.Application ------> HRMS.Domain
    |                    ^
    v                    |
HRMS.Infrastructure ----+
          |
          +-------------> HRMS.Domain
```

Business rules must not be placed in controllers/endpoints. API endpoints should validate transport input and delegate to application use cases. Infrastructure implements interfaces declared by Application.

## Local setup

From the repository root:

```powershell
dotnet restore HRMS.sln
dotnet user-secrets init --project src/backend/HRMS.API
dotnet user-secrets set "ConnectionStrings:Hrms" "Server=Nithin\SQLEXPRESS01;Database=HRMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;" --project src/backend/HRMS.API
dotnet build HRMS.sln
dotnet run --project src/backend/HRMS.API
```

The readiness endpoint opens a real SQL connection. The liveness endpoint does not depend on SQL Server.

- `GET /api/v1/health/live`
- `GET /api/v1/health/ready`
- `GET /api/v1/version`

Do not commit secrets. The empty connection-string value in appsettings is intentional; local configuration belongs in User Secrets. Use trusted certificates outside local development.

## Persistence rules

- Use Dapper with parameterized commands and stored procedures.
- Do not concatenate user input into SQL.
- Do not expose `SqlConnection` or Dapper types outside Infrastructure.
- Resolve tenant identity from authenticated server-side claims, never from a caller-provided tenant identifier alone.
- Use cancellation tokens for database operations.
- Keep transactions around use cases that require atomic multi-step writes.

## API organization

The API uses conventional ASP.NET Core MVC controllers grouped by business module under `HRMS.API/Modules`. Authentication, Company Management, Employee Management, Leave Management and Timesheet Management are separate controller areas. Platform health/version endpoints are kept under Platform.

`Program.cs` is the composition root and maps controllers with `MapControllers()`; business routes must not be added inline there. See [API Controller and Module Organization](../../docs/architecture/API-Controller-and-Module-Organization.md) for routing, authorization, tenant-context and future module-extraction conventions.
