namespace HRMS.Application.Employees;

public interface IEmployeeRepository
{
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid tenantId, Guid companyId, string? search, string? status, CancellationToken cancellationToken);
    Task<EmployeeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateStatusAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeStatusRequest request, CancellationToken cancellationToken);
}
