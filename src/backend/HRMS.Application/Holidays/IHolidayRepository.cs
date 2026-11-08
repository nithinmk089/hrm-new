namespace HRMS.Application.Holidays;

/// <summary>Tenant- and company-scoped persistence operations for holidays.</summary>
public interface IHolidayRepository
{
    Task<IReadOnlyList<HolidayDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
    Task<HolidayDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid holidayId, CancellationToken cancellationToken);
    Task<HolidayDto?> GetByDateAsync(Guid tenantId, Guid companyId, DateTime holidayDate, CancellationToken cancellationToken);
    Task<HolidayDto> CreateAsync(Guid tenantId, Guid companyId, CreateHolidayRequest request, CancellationToken cancellationToken);
    Task<HolidayDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid holidayId, UpdateHolidayRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid holidayId, byte[] rowVersion, CancellationToken cancellationToken);
}
