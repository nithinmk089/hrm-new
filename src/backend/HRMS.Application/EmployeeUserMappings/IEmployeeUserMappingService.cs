namespace HRMS.Application.EmployeeUserMappings;

public interface IEmployeeUserMappingService
{
    Task<IReadOnlyList<AvailableTenantUserDto>> GetAvailableTenantUsersAsync(Guid tenantId, Guid companyId, string? search, CancellationToken cancellationToken = default);
    Task<EmployeeUserMappingDto?> GetByEmployeeAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken = default);
    Task<MyEmployeeMappingDto?> GetMineAsync(Guid tenantId, Guid companyId, Guid userId, CancellationToken cancellationToken = default);
    Task<EmployeeUserMappingDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeUserMappingRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid mappingId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
