namespace HRMS.Application.Holidays;

public sealed record CreateHolidayRequest(
    string HolidayName,
    DateTime HolidayDate,
    string? Description,
    bool IsOptional);

public sealed record UpdateHolidayRequest(
    string HolidayName,
    DateTime HolidayDate,
    string? Description,
    bool IsOptional,
    byte[] RowVersion);

public sealed record DeactivateHolidayRequest(byte[] RowVersion);
