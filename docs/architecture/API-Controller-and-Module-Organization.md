# API Controller and Module Organization

## Architecture decision

HRMS V1 uses conventional ASP.NET Core MVC controllers organized by business module. The application remains a modular monolith: one API host and deployment, with explicit business-module boundaries.

Controllers are HTTP adapters only. They must not contain SQL, Dapper calls, stored-procedure names, or business rules. They read authenticated identity/scope information, call Application services, and translate results into HTTP responses.

## API project layout

```text
HRMS.API/
├── Modules/
│   ├── Authentication/
│   │   └── Controllers/
│   │       └── AuthController.cs
│   ├── CompanyManagement/
│   │   └── Controllers/
│   │       ├── CompaniesController.cs
│   │       ├── DepartmentsController.cs
│   │       ├── OfficesController.cs
│   │       └── DesignationsController.cs
│   ├── EmployeeManagement/
│   │   └── Controllers/
│   ├── LeaveManagement/
│   │   └── Controllers/
│   ├── TimesheetManagement/
│   │   └── Controllers/
│   ├── Platform/
│   │   └── Controllers/
│   │       ├── HealthController.cs
│   │       └── PlatformController.cs
│   └── Shared/
│       └── ClaimsPrincipalExtensions.cs
├── Authorization/
├── ErrorHandling/
└── Program.cs
```

## Controller conventions

- Use `[ApiController]` and attribute routing.
- Keep route prefixes versioned under `/api/v1`.
- Use one controller per cohesive resource or use-case group; avoid giant cross-module controllers.
- Use constructor injection for Application interfaces.
- Use `CancellationToken` on asynchronous actions and pass it through to Application.
- Use `[Authorize(Policy = "Permission:<permission-code>")]` for permission-protected actions.
- Use `[AllowAnonymous]` only for intentionally public endpoints.
- Derive TenantId from the validated ClaimsPrincipal. Never accept TenantId as a trusted request-body or query parameter.
- Preserve established response contracts, status codes, routes and permission codes during refactoring.
- Keep refresh tokens in Secure/HttpOnly/SameSite cookies; never return them in response bodies or log them.
- Do not inject repositories or database connections into controllers.

## Module boundaries and future extraction

The module folders establish API ownership boundaries; they do not by themselves make modules independent services. To preserve future extraction options:

1. Each business module owns its use cases, domain rules and persistence abstractions in the corresponding Application/Domain/Infrastructure module namespaces.
2. A module must not query another module's tables through its own repository.
3. Cross-module workflows should use explicit application contracts. Introduce integration events when asynchronous decoupling is justified.
4. Avoid circular project or namespace dependencies between modules.
5. Keep authentication/identity concerns separate from business-module authorization.
6. Extract a module into a microservice only when independent deployment, scaling, ownership or availability requirements justify the operational cost. At extraction time, establish service-owned data and explicit network contracts.

## Program.cs responsibility

`Program.cs` is the composition root. It configures dependency injection, authentication, authorization, CORS, Swagger, middleware, operational commands (migration/seed), and calls `MapControllers()`. Business endpoint implementations do not belong there.
