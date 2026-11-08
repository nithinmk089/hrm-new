# Local development seed data

The development bootstrap creates a local tenant, global administrator identity,
active tenant membership, demo company, tenant-scoped System Administrator
role, grants every currently active permission to that role, and assigns the
role to the user at tenant scope.

It is a repeatable bootstrap operation, not a schema migration. It does not
run automatically during API startup and it must never be run in Production.

## Prerequisites

1. Apply database migrations 001 through 008. Migration 008 adds the department, office and designation permissions required by the development administrator role.
2. Configure the API connection string and JWT settings using .NET User Secrets.
3. Use the Development environment.
4. Set DevelopmentSeed:Enabled to true.
5. Set a local bootstrap password only when the user does not already exist.
   The password must be at least 12 characters.

Default local identity and tenant settings:

| Setting | Value |
|---|---|
| Tenant code | 001 |
| Tenant name | Demo Company |
| User email | nithinmk089@gmail.com |
| Display name | Development Administrator |
| Company code | DEMO |
| Company name | Demo Technologies |
| Role code | SYSTEM_ADMIN |
| Role name | System Administrator |

## Run from PowerShell

Set the non-secret options in User Secrets:

~~~powershell
dotnet user-secrets set "DevelopmentSeed:Enabled" "true" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:TenantCode" "001" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:TenantName" "Demo Company" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:Email" "nithinmk089@gmail.com" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:DisplayName" "Development Administrator" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:CompanyCode" "DEMO" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:CompanyLegalName" "Demo Technologies Private Limited" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:CompanyDisplayName" "Demo Technologies" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:CountryCode" "IN" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:CurrencyCode" "INR" --project .\src\backend\HRMS.API\HRMS.API.csproj
dotnet user-secrets set "DevelopmentSeed:TimeZoneId" "Asia/Kolkata" --project .\src\backend\HRMS.API\HRMS.API.csproj

$securePassword = Read-Host "Enter a new development password (12+ characters)" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $env:DevelopmentSeed__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    dotnet run --project .\src\backend\HRMS.API\HRMS.API.csproj -- --seed-development
}
finally {
    Remove-Item Env:\DevelopmentSeed__Password -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $securePassword.Dispose()
}
~~~

The password is read without echoing it and passed to the process through a
temporary environment variable. It is not written to User Secrets, source
control, or console output.

The seed operation does not reset an existing user's password. If the user
already exists, the password input is not used. Existing disabled/deleted
users, inactive tenants, inactive memberships, or conflicting company codes
cause the operation to stop instead of silently reactivating or changing them.

## Verify

After the command reports success, run:

~~~sql
SELECT TenantId, TenantCode, TenantName, Status
FROM platform.Tenants
WHERE TenantCode = '001';

SELECT UserId, Email, DisplayName, IsEnabled, IsDeleted
FROM security.Users
WHERE NormalizedEmail = UPPER('nithinmk089@gmail.com');

SELECT m.MembershipId, m.TenantId, m.UserId, m.MembershipStatus, m.IsDefault
FROM security.UserTenantMemberships AS m
INNER JOIN platform.Tenants AS t ON t.TenantId = m.TenantId
WHERE t.TenantCode = '001';

SELECT CompanyId, TenantId, CompanyCode, LegalName, IsActive
FROM hrms.Companies
WHERE CompanyCode = 'DEMO';

SELECT r.RoleId, r.RoleCode, r.RoleName, r.IsActive
FROM security.Roles AS r
INNER JOIN platform.Tenants AS t ON t.TenantId = r.TenantId
WHERE t.TenantCode = '001' AND r.RoleCode = 'SYSTEM_ADMIN';

SELECT COUNT(*) AS GrantedActivePermissions
FROM security.RolePermissions AS rp
INNER JOIN security.Roles AS r
    ON r.TenantId = rp.TenantId AND r.RoleId = rp.RoleId
INNER JOIN platform.Tenants AS t ON t.TenantId = r.TenantId
WHERE t.TenantCode = '001' AND r.RoleCode = 'SYSTEM_ADMIN';

SELECT UserId, RoleId, ScopeType, CompanyId, OfficeId, ExpiresAtUtc
FROM security.UserRoleAssignments
WHERE TenantId = (SELECT TenantId FROM platform.Tenants WHERE TenantCode = '001')
  AND UserId = (
      SELECT UserId FROM security.Users
      WHERE NormalizedEmail = UPPER('nithinmk089@gmail.com')
  );
~~~

Then start the API normally and test login, /api/v1/auth/me, the company endpoints, and the organization endpoints after applying migration 009. Never paste passwords, access tokens, refresh tokens, or signing
keys into chat or logs.
