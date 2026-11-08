namespace HRMS.Application.Designations;

/// <summary>Use cases for managing designations within an authenticated tenant and company.</summary>
public interface IDesignationService
{
    Task<IReadOnlyList<DesignationDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<DesignationDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid designationId, CancellationToken cancellationToken = default);
    Task<DesignationDto> CreateAsync(Guid tenantId, Guid companyId, CreateDesignationRequest request, CancellationToken cancellationToken = default);
    Task<DesignationDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid designationId, UpdateDesignationRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid designationId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
