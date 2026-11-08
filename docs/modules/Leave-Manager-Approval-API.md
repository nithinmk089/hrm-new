# Leave Manager Approval API

## Endpoints

All endpoints require authentication and the relevant company-scoped permission.

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/api/v1/companies/{companyId}/leave-requests/pending-approvals` | `leave-requests.read` | List pending requests for the signed-in manager's current direct reports |
| POST | `/api/v1/companies/{companyId}/leave-requests/{leaveRequestId}/approve` | `leave-requests.approve` | Approve a pending request |
| POST | `/api/v1/companies/{companyId}/leave-requests/{leaveRequestId}/reject` | `leave-requests.reject` | Reject a pending request |

## Decision request

The RowVersion is the Base64 representation returned by the queue endpoint.

```json
{
  "rowVersion": "AAAAAAAAB9E=",
  "comments": "Approved"
}
```

For rejection, comments are required. Comments are optional for approval. The server resolves the manager employee from the authenticated user mapping; manager or employee IDs are never accepted from the request body.

## Workflow guarantees

- The queue is restricted to pending requests from employees whose current effective reporting assignment points to the authenticated manager.
- A decision rechecks the active tenant membership, manager mapping, current reporting assignment, request status and RowVersion inside the SQL transaction.
- A manager cannot decide their own request.
- Approval moves each reserved balance allocation from ReservedDays to UsedDays and writes a ReservationRelease and Usage ledger entry.
- Rejection decreases ReservedDays and writes a negative ReservationRelease ledger entry.
- Request status, reviewer, review timestamp, comments, approval history, balance aggregates and ledger entries commit or roll back together.
- A stale RowVersion or request already decided by another actor returns HTTP 409 through the API exception handler.

## Responses

- `200 OK`: queue or decision result.
- `400 Bad Request`: invalid RowVersion or comments.
- `401 Unauthorized`: missing/invalid access token.
- `403 Forbidden`: missing permission.
- `404 Not Found`: request not found in the authenticated company.
- `409 Conflict`: request is not pending, stale RowVersion, manager mismatch, or inconsistent reservation.

The SQL procedure uses tenant and company predicates for every request, manager, balance and ledger operation.
