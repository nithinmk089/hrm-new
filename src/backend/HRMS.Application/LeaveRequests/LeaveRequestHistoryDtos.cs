namespace HRMS.Application.LeaveRequests;

public sealed record EmployeeLeaveRequestItem(
    Guid LeaveRequestId,
    Guid CompanyId,
    Guid EmployeeId,
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateTime FromDate,
    DateTime ToDate,
    bool IsHalfDay,
    string? HalfDayPeriod,
    decimal DaysRequested,
    string Reason,
    string Status,
    DateTime SubmittedAtUtc,
    Guid? ReviewedByEmployeeId,
    DateTime? ReviewedAtUtc,
    string? ReviewComments,
    DateTime? CancelledAtUtc,
    byte[] RowVersion);

public sealed record LeaveRequestHistoryItem(
    string Action,
    Guid ActorEmployeeId,
    string? Comments,
    DateTime ActionAtUtc);

public sealed record LeaveRequestCancellationRequest(byte[] RowVersion, string? Comments);

public sealed record LeaveRequestCancellationResult(
    Guid LeaveRequestId,
    string Status,
    DateTime CancelledAtUtc,
    byte[] RowVersion);
