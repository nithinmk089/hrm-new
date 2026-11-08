# Leave Type Management API

## Scope

Leave Type Management configures the leave categories available to employees
within one company. Examples include Annual Leave, Sick Leave, Casual Leave,
Unpaid Leave and Maternity Leave.

This module configures leave policies only. Leave balances, accruals, requests,
approval workflows, holidays applied to working-day calculations and employee
eligibility are implemented in subsequent Leave Management work.

## Endpoints

Base route: `/api/v1/companies/{companyId}/leave-types`

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/` | `leave-types.read` | List leave types |
| GET | `/{leaveTypeId}` | `leave-types.read` | Get one leave type |
| POST | `/` | `leave-types.create` | Create a leave type |
| PUT | `/{leaveTypeId}` | `leave-types.update` | Update a leave type |
| PATCH | `/{leaveTypeId}/deactivate` | `leave-types.deactivate` | Deactivate a leave type |

All endpoints require authentication and the relevant permission. TenantId is
derived from the validated access-token claims. TenantId and CompanyId are
included in all repository procedure predicates.

## Create request

```json
{
  "leaveTypeCode": "ANNUAL",
  "leaveTypeName": "Annual Leave",
  "description": "Paid annual leave entitlement",
  "isPaid": true,
  "requiresApproval": true,
  "annualEntitlementDays": 20,
  "allowHalfDay": true,
  "allowCarryForward": true,
  "maxCarryForwardDays": 5
}
```

For an entitlement without a configured annual cap, set
`annualEntitlementDays` to null. When carry-forward is disabled,
`maxCarryForwardDays` must be zero.

Update requests include the current eight-byte RowVersion returned by GET or
POST. Deactivate requests contain the current RowVersion only.

## Configuration rules

- LeaveTypeCode is required, trimmed, uppercased and limited to 30 characters.
- LeaveTypeName is required and limited to 100 characters.
- Description is optional and limited to 500 characters.
- AnnualEntitlementDays is nullable; when supplied it must be from 0 to 366.
- MaxCarryForwardDays must be from 0 to 366.
- When AllowCarryForward is false, MaxCarryForwardDays must be zero.
- When AllowCarryForward is true, MaxCarryForwardDays must be positive.
- IsPaid, RequiresApproval and AllowHalfDay are explicit policy switches.
- Active leave-type codes are unique within a company.
- Updates use RowVersion concurrency; stale writes return HTTP 409.
- Deactivation is a soft operational state change, not physical deletion.

## Database

Migration `015_LeaveTypeManagement.sql` creates `hrms.LeaveTypes` and the
tenant/company-scoped stored procedures. Migration
`016_LeaveTypePermissions.sql` adds the four leave-type permissions. Run the
standard database migration command before using these endpoints.
