namespace HRMS.Application.Holidays;

/// <summary>Company holiday data returned by the application layer.</summary>
public sealed record HolidayDto(
    Guid HolidayId,
    Guid CompanyId,
    string HolidayName,
    DateTime HolidayDate,
    string? Description,
    bool IsOptional,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
