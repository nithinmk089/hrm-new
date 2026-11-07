# Repository instructions for AI coding agents

## Before changing code

1. Read this file and the relevant documents under `docs/`.
2. Inspect the existing implementation before creating, replacing or moving files.
3. Confirm the current branch and working tree where local tools are available.
4. Follow existing patterns unless they conflict with an explicit architecture or security standard.
5. Make the smallest coherent change and report exactly what changed and what was not verified.

## Product boundaries

V1 includes employee management, leave management, timesheet management, and company administration (departments, offices, designations, holidays, asset types, assets, leave types and company policies). Do not silently add later-phase modules to V1.

## Architecture

- Use a modular monolith with clear module boundaries.
- Apply Clean Architecture dependency direction: Domain has no infrastructure dependencies; Application depends on Domain abstractions; Infrastructure implements external concerns; API is the composition/presentation boundary.
- Do not introduce microservices, message brokers, Kubernetes or distributed transactions for V1 without an approved requirement.
- Keep module-specific business logic within its module. Shared code must be genuinely cross-cutting.
- Use dependency injection and explicit interfaces at meaningful boundaries; avoid abstractions that add no value.

## Backend standards

- Target .NET 10 and ASP.NET Core.
- Use asynchronous I/O APIs and pass CancellationToken through request paths.
- Use Dapper with parameterized SQL and stored procedures. Do not concatenate untrusted input into SQL.
- Keep controllers/endpoints thin. Put use cases and business rules in Application/Domain.
- Validate requests at the application boundary and return consistent Problem Details responses.
- Do not return database rows/entities directly. Use explicit request/response contracts.
- Use nullable reference types, analyzers and warnings-as-errors where practical.
- Do not swallow exceptions. Log with structured properties and preserve the original exception.
- Never log passwords, access tokens, refresh tokens, secrets or sensitive employee data.

## Database standards

- Target SQL Server 2022+ (compatibility level 160).
- Every tenant-owned business table must include TenantID and enforce tenant scoping.
- Include standard audit fields and optimistic concurrency/versioning where appropriate.
- Use soft deletion for business records where retention and legal requirements permit; do not soft-delete immutable audit history.
- Use explicit column lists; never use SELECT *.
- Parameterize all values. Validate dynamic sort/filter identifiers against allowlists.
- Add indexes based on real query patterns and verify execution plans for high-volume queries.
- Store schema changes as ordered, repeatable, reviewed SQL scripts. Do not make undocumented production schema changes.
- Do not use triggers for ordinary business workflows. Use them only for a documented database-integrity requirement.

## Frontend standards

- Use Angular standalone APIs and strict TypeScript settings.
- Keep components focused on presentation and interaction; isolate API access and business orchestration in services/facades.
- Use reactive forms for non-trivial forms and typed models for API contracts.
- Use Bootstrap 5 with a restrained enterprise visual language. Avoid arbitrary colors and one-off CSS duplication.
- Provide loading, empty, validation, error and success states.
- Ensure keyboard accessibility, semantic HTML and WCAG 2.2 AA-oriented contrast/focus behavior.
- Never place secrets or authorization decisions in browser code. The API is authoritative.

## Security and privacy

- Enforce authorization on the server for every protected operation.
- Never trust TenantID, employee identity, role or permission values supplied by the browser without validating them against the authenticated principal and server-side context.
- Apply least privilege, secure headers, input validation, output encoding and safe file handling.
- Do not commit secrets, real employee records or production exports. Use synthetic test data.
- Review changes involving authentication, authorization, tenant isolation, personal data, file uploads or audit history especially carefully.

## Tests and quality

- Add unit tests for business rules and validators.
- Add integration tests for persistence, API authorization and tenant isolation.
- Add regression tests for bug fixes.
- Do not claim tests, builds, migrations or deployments passed unless they were actually run and the result was observed.
- Do not disable or weaken tests to make a change pass without explaining and justifying the change.

## Change discipline

- Do not overwrite user work or unrelated files.
- Do not change public API contracts or database schemas without updating their documentation and tests.
- Prefer explicit, reversible changes.
- Summarize changed files, key decisions, tests run, limitations and next steps in the final response.
