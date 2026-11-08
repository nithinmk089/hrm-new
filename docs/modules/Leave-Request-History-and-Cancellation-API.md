# Employee Leave Request History and Cancellation API

## Endpoints

All endpoints require authentication and company-scoped permissions.

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/api/v1/companies/{companyId}/leave-requests/mine` | `leave-requests.read` | List the signed-in employee's latest 100 requests |
| GET | `/api/v1/companies/{companyId}/leave-requests/mine/{leaveRequestId}/history` | `leave-requests.read` | Read action history for the signed-in employee's request |
| POST | `/api/v1/companies/{companyId}/leave-requests/{leaveRequestId}/cancel` | `leave-requests.cancel` | Cancel the signed-in employee's request |

## Cancellation request

The RowVersion is the Base64 representation returned by the employee request list.

```json
{
  "rowVersion": "AAAAAAAAB9E=",
  "comments": "My plans have changed."
}
```

Comments are optional and are recorded in the cancellation history. Existing manager review comments are preserved.

## Cancellation rules

- The requester is resolved from the authenticated user-to-employee mapping. Employee IDs are never accepted from the client.
- Only Pending and Approved requests can be cancelled.
- Pending cancellation releases the annual balance reservation.
- Approved cancellation is allowed only before the request's start date (UTC calendar date).
- Approved cancellation restores used balance.
- Rejected, already-cancelled, and in-progress/past approved requests cannot be cancelled.
- The request RowVersion is checked inside the transaction.
- Request status, cancellation timestamp, balance aggregates, ledger entries and Cancelled history are committed atomically.
- A negative ReservationRelease ledger entry reverses a pending reservation; a negative Restoration ledger entry reverses approved usage.
- The employee can only read their own requests and their own request history.

## Responses

- `200 OK`: request list, history or cancellation result.
- `400 Bad Request`: invalid RowVersion or comments.
- `401 Unauthorized`: missing/invalid access token.
- `403 Forbidden`: missing permission.
- `409 Conflict`: request is not cancellable, stale RowVersion, missing mapping or inconsistent balance ledger.
