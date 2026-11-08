namespace HRMS.Application.Employees;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid tenantId, Guid companyId, string? search, string? status, CancellationToken cancellationToken = default);
    Task<EmployeeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeStatusRequest request, CancellationToken cancellationToken = default);
}
