# Authentication API (V1)

## Configuration

The API uses HMAC-SHA256 signed JWT access tokens. The issuer and audience are
configured in `appsettings.json` under `Authentication:Jwt`. The signing key
must never be committed to source control.

Generate a random 48-byte key and store it in .NET User Secrets:

```powershell
$jwtKey = [Convert]::ToBase64String(
    [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48)
)

dotnet user-secrets set "Authentication:Jwt:SigningKey" $jwtKey --project .\src\backend\HRMS.API\HRMS.API.csproj
```

The generated Base64 text is used as UTF-8 key material by both token issuance
and validation. Do not paste it into source files, tickets, logs, or chat.
The access-token lifetime defaults to 10 minutes and must be between 1 and 30.

Refresh-token lifetime defaults to 14 days and is configurable using
`Authentication:RefreshToken:LifetimeDays` (allowed range 1–90).

For browser clients on a different origin, configure the exact frontend origins
in `Authentication:AllowedOrigins`, for example:

```json
{
  "Authentication": {
    "AllowedOrigins": [
      "http://localhost:4200"
    ]
  }
}
```

Do not use wildcard origins. Configure the production HTTPS origin explicitly.

## Login

```http
POST /api/v1/auth/login
Content-Type: application/json
```

Request:

```json
{
  "email": "person@example.com",
  "password": "your-password",
  "tenantCode": "ACME"
}
```

The tenant code is a selector, not proof of access. The server verifies the
global identity, enabled account, active tenant, and active membership using
the canonical `security.Users`, `security.UserTenantMemberships`, and
`platform.Tenants` tables. The client never supplies a trusted TenantId.

Successful response returns a short-lived bearer access token and selected
tenant details. The refresh token is set only in an HttpOnly cookie named
`hrms_refresh`; it is never included in the JSON response. Invalid credentials,
inactive memberships, disabled users, and locked accounts return HTTP 401.

Five failed password attempts trigger a 15-minute account lockout. Successful
login resets the failed-attempt counter. Password hashes requiring an upgrade
are rehashed after successful verification.

## Refresh

```http
POST /api/v1/auth/refresh
Cookie: hrms_refresh=<HttpOnly-cookie>
```

The endpoint atomically consumes the current refresh token and issues a new
access token plus a replacement refresh-token cookie. Reuse of an already
rotated token triggers family revocation in the repository. Missing, expired,
invalid, or reused tokens return HTTP 401 and clear the cookie.

The cookie is HttpOnly, SameSite=Strict, scoped to `/api/v1/auth`, and Secure
outside Development (or when Development is accessed over HTTPS). Refresh and
logout requests with an Origin header are checked against the configured
allowed origins. Requests without an Origin header are permitted for non-browser
clients; browser clients must use HTTPS outside local development.

## Logout

```http
POST /api/v1/auth/logout
Cookie: hrms_refresh=<HttpOnly-cookie>
```

Logout revokes the presented refresh token when present and clears the cookie.
The endpoint is idempotent from the client's perspective.

## Current identity

```http
GET /api/v1/auth/me
Authorization: Bearer <access-token>
```

This endpoint validates the JWT and returns the authenticated user, tenant,
and membership identifiers. Permission and company/office scope authorization
are implemented in a later increment.

## Client handling

- Keep access tokens in application memory; do not store them in localStorage
  or sessionStorage.
- Send refresh/logout requests with credentials enabled when calling the API
  cross-origin.
- Never expose or log the refresh cookie, access token, password, or signing key.
- Configure exact allowed origins; do not use wildcard origins.
