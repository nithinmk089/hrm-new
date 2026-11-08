namespace HRMS.Application.Departments;

/// <summary>Use cases for managing departments within an authenticated tenant and company.</summary>
public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<DepartmentDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid departmentId, CancellationToken cancellationToken = default);
    Task<DepartmentDto> CreateAsync(Guid tenantId, Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task<DepartmentDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid departmentId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
