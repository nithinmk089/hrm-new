namespace HRMS.Application.Timesheets;

public sealed record TimesheetEntryDto(
    Guid TimesheetEntryId, DateOnly WorkDate, string WorkCategory,
    string Description, decimal Hours, byte[] RowVersion);

public sealed record TimesheetDetailDto(
    Guid TimesheetId, DateOnly WeekStartDate, DateOnly WeekEndDate, string Status,
    DateTime? SubmittedAtUtc, Guid? ReviewedByEmployeeId, DateTime? ReviewedAtUtc,
    string? ReviewComments, byte[] RowVersion, decimal TotalHours,
    IReadOnlyList<TimesheetEntryDto> Entries);

public sealed record TimesheetSummaryDto(
    Guid TimesheetId, DateOnly WeekStartDate, DateOnly WeekEndDate, string Status,
    DateTime? SubmittedAtUtc, DateTime? ReviewedAtUtc, string? ReviewComments,
    byte[] RowVersion, decimal TotalHours);

public sealed record TimesheetHistoryItem(
    Guid TimesheetId, string Action, Guid ActorEmployeeId, string? Comments, DateTime ActionAtUtc);

public sealed record TimesheetHistoryResponse(
    IReadOnlyList<TimesheetSummaryDto> Timesheets,
    IReadOnlyList<TimesheetHistoryItem> History);

public sealed record TimesheetCreateRequest(DateOnly WeekStartDate);

public sealed record TimesheetCreateResult(
    Guid TimesheetId, DateOnly WeekStartDate, DateOnly WeekEndDate, string Status, byte[] RowVersion);

public sealed record TimesheetEntryInput(
    DateOnly WorkDate, string WorkCategory, string Description, decimal Hours);

public sealed record SaveTimesheetEntriesRequest(
    byte[] RowVersion, IReadOnlyList<TimesheetEntryInput> Entries);

public sealed record SaveTimesheetEntriesResult(
    Guid TimesheetId, string Status, byte[] RowVersion, decimal TotalHours);

public sealed record TimesheetSubmitRequest(byte[] RowVersion);

public sealed record TimesheetSubmitResult(
    Guid TimesheetId, string Status, DateTime SubmittedAtUtc, byte[] RowVersion);

public sealed record TimesheetDecisionRequest(byte[] RowVersion, string? Comments);

public sealed record TimesheetDecisionResult(
    Guid TimesheetId, string Status, Guid ReviewedByEmployeeId,
    DateTime ReviewedAtUtc, byte[] RowVersion);

public sealed record TimesheetApprovalQueueItem(
    Guid TimesheetId, Guid EmployeeId, string EmployeeCode,
    string FirstName, string LastName, DateOnly WeekStartDate,
    DateOnly WeekEndDate, DateTime SubmittedAtUtc, decimal TotalHours, byte[] RowVersion);
