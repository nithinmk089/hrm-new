namespace HRMS.Application.WorkingWeeks;

public sealed record WorkingWeekDayDto(byte DayOfWeek, bool IsWorkingDay);

public sealed record WorkingWeekDto(
    Guid CompanyId,
    Guid? OfficeId,
    bool IsConfigured,
    bool IsInherited,
    IReadOnlyList<WorkingWeekDayDto> Days);

public sealed record SaveWorkingWeekRequest(IReadOnlyList<WorkingWeekDayDto> Days);
