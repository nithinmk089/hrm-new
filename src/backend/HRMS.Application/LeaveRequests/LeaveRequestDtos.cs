namespace HRMS.Application.LeaveRequests;

public sealed record SubmitLeaveRequest(
    Guid LeaveTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsHalfDay,
    string? HalfDayPeriod,
    string Reason);

public sealed record LeaveRequestDto(
    Guid LeaveRequestId,
    Guid CompanyId,
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
    string Status,
    DateTime SubmittedAtUtc,
    byte[] RowVersion);

public sealed record LeaveRequestSubmissionResult(
    LeaveRequestDto Request,
    IReadOnlyList<LeaveYearReservationDto> YearAllocations,
    IReadOnlyList<DateOnly> ExcludedDates);

public sealed record LeaveYearReservationDto(short LeaveYear, decimal Days);
