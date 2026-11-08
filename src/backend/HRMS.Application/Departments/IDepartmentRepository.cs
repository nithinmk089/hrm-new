namespace HRMS.Application.Departments;

/// <summary>Tenant- and company-scoped persistence operations for departments.</summary>
public interface IDepartmentRepository
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
    Task<DepartmentDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid departmentId, CancellationToken cancellationToken);
    Task<DepartmentDto?> GetByCodeAsync(Guid tenantId, Guid companyId, string departmentCode, CancellationToken cancellationToken);
    Task<bool> ParentExistsAsync(Guid tenantId, Guid companyId, Guid parentDepartmentId, CancellationToken cancellationToken);
    Task<DepartmentDto> CreateAsync(Guid tenantId, Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken);
    Task<DepartmentDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid departmentId, byte[] rowVersion, CancellationToken cancellationToken);
}
