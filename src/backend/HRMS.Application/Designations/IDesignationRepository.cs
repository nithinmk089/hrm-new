namespace HRMS.Application.Designations;

/// <summary>Tenant- and company-scoped persistence operations for designations.</summary>
public interface IDesignationRepository
{
    Task<IReadOnlyList<DesignationDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
    Task<DesignationDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid designationId, CancellationToken cancellationToken);
    Task<DesignationDto?> GetByCodeAsync(Guid tenantId, Guid companyId, string designationCode, CancellationToken cancellationToken);
    Task<DesignationDto> CreateAsync(Guid tenantId, Guid companyId, CreateDesignationRequest request, CancellationToken cancellationToken);
    Task<DesignationDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid designationId, UpdateDesignationRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid designationId, byte[] rowVersion, CancellationToken cancellationToken);
}
