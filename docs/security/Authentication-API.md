# Authentication API (V1)

## Configuration

The API uses HMAC-SHA256 signed JWT access tokens. The issuer and audience are
configured in `appsettings.json` under `Authentication:Jwt`. The signing key
must never be committed to source control.

Generate a random 48-byte key and store it in .NET User Secrets from the
repository root:

```powershell
$jwtKey = [Convert]::ToBase64String(
    [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48)
)

dotnet user-secrets set "Authentication:Jwt:SigningKey" $jwtKey --project .\src\backend\HRMS.API\HRMS.API.csproj
```

The generated Base64 text is used as UTF-8 key material by both token issuance
and validation. Do not paste it into source files, tickets, logs, or chat.

The access-token lifetime must be between 1 and 30 minutes. The default is
10 minutes.

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

Successful response includes an access token, expiry, user ID, and selected
tenant details. Invalid credentials, inactive memberships, disabled users,
and currently locked accounts return the same HTTP 401 response.

Five failed password attempts trigger a 15-minute account lockout. A successful
login resets the failed-attempt counter. Password hashes requiring an upgrade
are rehashed after successful verification.

## Current identity

```http
GET /api/v1/auth/me
Authorization: Bearer <access-token>
```

This endpoint validates the JWT and returns the authenticated user, tenant,
and membership identifiers. It is a token-validation diagnostic endpoint;
permission and company/office scope authorization are implemented in a later
increment.

## Client handling

- Use HTTPS outside local development.
- Keep access tokens in application memory; do not store them in localStorage
  or sessionStorage.
- Access tokens are short-lived. Refresh-token rotation and secure refresh
  cookie handling are not part of this increment and must be implemented
  before a production browser session lifecycle is considered complete.
- Never log passwords, access tokens, or signing keys.
