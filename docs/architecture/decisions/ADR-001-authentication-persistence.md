# ADR-001: Authentication and authorization persistence

- Status: Proposed for V1 implementation
- Date: 2026-11-07

## Context

HRMS V1 needs tenant-aware user accounts, roles, permissions, access-token authentication and refresh-token revocation. The database baseline uses SQL Server, Dapper and stored procedures. The design should avoid coupling the application to EF Core schema migrations while still using a vetted password-hashing implementation.

## Decision

1. Persist tenant-scoped users, roles, user-role assignments and refresh-token hashes in SQL Server.
2. Maintain a global permission catalog and tenant-scoped role-permission assignments.
3. Use a vetted .NET password-hashing implementation (for example, the ASP.NET Core Identity password hasher package) as a cryptographic library; do not require EF Core or Identity's EF persistence store.
4. Never store plaintext passwords or raw refresh tokens. Store only a cryptographic hash of a high-entropy refresh token.
5. Use short-lived access tokens. Refresh tokens must rotate on use, be revocable, expire, and have reuse detection.
6. For browser clients, prefer Secure, HttpOnly, SameSite cookies for refresh tokens. Define CSRF protections and cookie domain/path policy before implementation.
7. Enforce role/permission checks and tenant data-scope checks on the API. Angular route guards are only a user-experience feature, not a security boundary.
8. Database access remains Dapper + stored procedures. User/role/permission and token persistence must use tenant-scoped stored procedures.

## Alternatives considered

- ASP.NET Core Identity with EF Core persistence: mature and feature-rich, but conflicts with the current Dapper/stored-procedure database baseline unless its persistence layer is customized.
- Fully custom authentication and password hashing: rejected because implementing cryptographic primitives and password storage from scratch is unsafe.
- External identity provider: potentially valuable for SSO, but adds setup/cost/dependency and is not required to establish the V1 foundation.

## Consequences

- The team owns the SQL schema and stored procedures for identity persistence.
- Password hashing, lockout, token rotation and account recovery still require careful implementation and security tests.
- MFA and enterprise SSO can be added later without changing the core tenant ownership model.
- This ADR must be reviewed before production authentication is enabled.
