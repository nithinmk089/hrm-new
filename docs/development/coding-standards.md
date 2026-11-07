# HRMS V1 — Coding Standards

## General

- Prefer readable, explicit code over clever or compressed code.
- Use consistent naming: PascalCase for .NET types/methods, camelCase for local variables/parameters and TypeScript members, kebab-case for Angular file names.
- Keep methods focused and avoid hidden side effects.
- Validate at trust boundaries; do not duplicate every validation rule in every layer.
- Use UTC for persisted timestamps. Make business-date calculations explicitly tenant/office time-zone aware.
- Avoid magic strings/numbers; centralize stable constants and strongly typed options.
- Do not commit secrets, generated build output, local settings containing credentials or real personal data.

## .NET / C#

- Target .NET 10; enable nullable reference types and implicit usings.
- Use asynchronous APIs for I/O and suffix asynchronous methods with `Async`.
- Accept and propagate `CancellationToken` in API-to-application-to-persistence calls.
- Use dependency injection for infrastructure adapters.
- Keep controllers/endpoints thin; application use cases coordinate work.
- Keep domain entities responsible for enforcing domain invariants.
- Use records for suitable immutable request/response DTOs; do not use persistence entities as API contracts.
- Use explicit access modifiers and avoid public mutable state.
- Prefer composition over inheritance.
- Use structured logging templates; never interpolate sensitive values into logs.
- Return consistent Problem Details responses from centralized exception handling.
- Do not catch exceptions unless adding meaningful handling, translation or context; preserve the original exception.
- Avoid static service locators and service-locator access through the DI container.

## Application and domain

- Model business concepts explicitly; use value objects/enums where they clarify invariants.
- Application layer owns use-case orchestration, authorization requirements and transaction boundaries.
- Domain layer must not reference ASP.NET Core, Dapper, SQL client packages or infrastructure implementations.
- Keep business rules independent of UI wording and HTTP status codes.
- Use explicit result/error patterns for expected business failures; reserve exceptions for exceptional conditions.
- Define transaction boundaries around a complete business operation, especially leave balance and approval transitions.

## SQL / data access

- Use Dapper and parameterized stored procedure calls.
- Use explicit parameter types/sizes where needed; avoid string-built SQL.
- Stored procedures must validate required inputs and apply tenant scoping.
- Use explicit column lists and stable result contracts.
- Avoid N+1 query patterns; batch where it is clearer and safe.
- Use transactions for multi-step writes that must succeed or fail together.
- Use optimistic concurrency/version checks for records that may be edited concurrently.
- Document indexes and their target query patterns.
- Do not use dynamic SQL unless necessary; identifiers must come from strict allowlists.
- Never trust a TenantID supplied by the browser; derive or validate it against trusted server context.

## Angular / TypeScript

- Use Angular standalone components and strict TypeScript.
- Prefer typed reactive forms for complex forms and typed API response models.
- Use signals or RxJS according to the state/lifecycle needs; avoid introducing multiple competing state patterns.
- Keep components focused on view behavior; put HTTP calls in feature services.
- Use lazy-loaded feature routes and route-level authorization as a UX layer only; API authorization remains mandatory.
- Unsubscribe or use lifecycle-aware RxJS patterns where required.
- Avoid `any`; use `unknown` and narrow types when the shape is not known.
- Keep shared components generic and feature components close to their feature.
- Display loading, empty, validation, error and success states.
- Use semantic HTML, keyboard navigation, visible focus and accessible labels.

## UI / CSS

- Bootstrap 5 is the layout and component baseline.
- Use a restrained enterprise palette and consistent spacing/typography.
- Keep shared design tokens and reusable styles centralized.
- Avoid broad global selectors and unnecessary `!important`.
- Do not encode permissions only by hiding UI controls; the API must enforce permissions.
- Ensure responsive layouts and usable tables/forms at supported viewport sizes.

## API conventions

- Use plural resource names and predictable HTTP semantics.
- Keep routes under `/api/v1`.
- Use DTOs with explicit validation and documented response shapes.
- Use pagination for collection endpoints; define stable sorting and filtering behavior.
- Return 400 for invalid input, 401 for unauthenticated, 403 for unauthorized, 404 for missing/inaccessible resources where appropriate, 409 for state/concurrency conflicts and 5xx for unexpected server failures.
- Do not reveal whether a sensitive record exists when that would leak information across tenant or employee scopes.

## Testing

- Unit-test domain rules and application use cases.
- Integration-test SQL procedures/repositories and API authorization with SQL Server-compatible infrastructure.
- Test tenant isolation, role/permission checks, concurrency, date boundaries, holidays and approval transitions.
- Use synthetic fixtures and deterministic clocks where time affects behavior.
- Name tests by behavior and expected outcome.
