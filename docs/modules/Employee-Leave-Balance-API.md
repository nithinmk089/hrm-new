# Employee Leave Balance API

Base route:
`/api/v1/companies/{companyId}/employees/{employeeId}/leave-balances`

- `GET ?year=2027` — `leave-balances.read`
- `POST /transactions` — `leave-balances.adjust`

The authenticated tenant and actor user are read from validated JWT claims.
The employee, company and leave type are checked server-side by tenant-scoped
stored procedures.

## Apply transaction

```json
{
  "leaveTypeId": "leave-type-guid",
  "leaveYear": 2027,
  "transactionType": "Opening",
  "daysDelta": 12,
  "notes": "Initial annual entitlement"
}
```

Supported transaction types are Opening, Accrual, CarryForward and Adjustment.
A positive transaction grants days; a negative Adjustment can reduce days
only when used plus reserved days remain within the resulting entitlement.

Each transaction updates the annual balance and writes a corresponding
EmployeeLeaveBalanceTransactions ledger row in one SQL transaction. Initial
balance rows are created only by a positive transaction. V1 does not silently
create balances when a leave request is submitted; HR must provision the
required employee/leave-type/year balance first.

Balance fields distinguish opening, accrual, carry-forward, adjustment, used,
reserved and available days. The ledger is append-only by application design.
