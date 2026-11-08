namespace HRMS.Application.LeaveRequests;

/// <summary>One effective-dated primary employment office assignment.</summary>
public sealed record LeaveOfficeAssignment(DateOnly StartDate, DateOnly? EndDate, Guid? OfficeId);

/// <summary>An active or inactive company holiday. Optional holidays are chargeable when requested.</summary>
public sealed record LeaveCalendarHoliday(DateOnly Date, bool IsOptional, bool IsActive);

/// <summary>One ISO weekday and whether it is normally a working day.</summary>
public sealed record LeaveWorkingWeekDay(byte DayOfWeek, bool IsWorkingDay);

public sealed record LeaveDayCalculationRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsHalfDay,
    bool AllowHalfDay,
    IReadOnlyList<LeaveWorkingWeekDay> CompanyWorkingWeek,
    IReadOnlyDictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>> OfficeWorkingWeeks,
    IReadOnlyList<LeaveOfficeAssignment> OfficeAssignments,
    IReadOnlyList<LeaveCalendarHoliday> Holidays);

public sealed record LeaveDayCalculationResult(
    decimal ChargeableDays,
    IReadOnlyList<DateOnly> ChargeableDates,
    IReadOnlyList<DateOnly> ExcludedDates);

/// <summary>Database-resolved context used by the leave submission use case.</summary>
public sealed record LeaveRequestCalculationContext(
    Guid EmployeeId,
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    bool RequiresApproval,
    bool AllowHalfDay,
    IReadOnlyList<LeaveWorkingWeekDay> CompanyWorkingWeek,
    IReadOnlyDictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>> OfficeWorkingWeeks,
    IReadOnlyList<LeaveOfficeAssignment> OfficeAssignments,
    IReadOnlyList<LeaveCalendarHoliday> Holidays);
