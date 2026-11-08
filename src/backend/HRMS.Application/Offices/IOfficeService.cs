namespace HRMS.Application.Offices;

/// <summary>Use cases for managing offices within an authenticated tenant and company.</summary>
public interface IOfficeService
{
    Task<IReadOnlyList<OfficeDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<OfficeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid officeId, CancellationToken cancellationToken = default);
    Task<OfficeDto> CreateAsync(Guid tenantId, Guid companyId, CreateOfficeRequest request, CancellationToken cancellationToken = default);
    Task<OfficeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid officeId, UpdateOfficeRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid officeId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
