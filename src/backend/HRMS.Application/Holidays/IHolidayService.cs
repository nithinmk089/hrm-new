namespace HRMS.Application.Holidays;

/// <summary>Use cases for managing holidays within an authenticated tenant and company.</summary>
public interface IHolidayService
{
    Task<IReadOnlyList<HolidayDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<HolidayDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid holidayId, CancellationToken cancellationToken = default);
    Task<HolidayDto> CreateAsync(Guid tenantId, Guid companyId, CreateHolidayRequest request, CancellationToken cancellationToken = default);
    Task<HolidayDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid holidayId, UpdateHolidayRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid holidayId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
