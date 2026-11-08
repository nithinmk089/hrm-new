namespace HRMS.Application.Offices;

/// <summary>Tenant- and company-scoped persistence operations for offices.</summary>
public interface IOfficeRepository
{
    Task<IReadOnlyList<OfficeDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
    Task<OfficeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid officeId, CancellationToken cancellationToken);
    Task<OfficeDto?> GetByCodeAsync(Guid tenantId, Guid companyId, string officeCode, CancellationToken cancellationToken);
    Task<OfficeDto> CreateAsync(Guid tenantId, Guid companyId, CreateOfficeRequest request, CancellationToken cancellationToken);
    Task<OfficeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid officeId, UpdateOfficeRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid officeId, byte[] rowVersion, CancellationToken cancellationToken);
}
