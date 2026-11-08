namespace HRMS.Application.LeaveRequests;

public sealed record LeaveApprovalDecisionRequest(byte[] RowVersion, string? Comments);

public sealed record LeaveApprovalQueueItem(
    Guid LeaveRequestId,
    Guid EmployeeId,
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsHalfDay,
    string? HalfDayPeriod,
    decimal DaysRequested,
    string Reason,
    DateTime SubmittedAtUtc,
    byte[] RowVersion);

public sealed record LeaveApprovalDecisionResult(
    Guid LeaveRequestId,
    string Status,
    Guid ReviewedByEmployeeId,
    DateTime ReviewedAtUtc,
    byte[] RowVersion);
