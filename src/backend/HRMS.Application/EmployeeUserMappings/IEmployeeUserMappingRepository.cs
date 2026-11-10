namespace HRMS.Application.EmployeeUserMappings;

public interface IEmployeeUserMappingRepository
{
    Task<IReadOnlyList<AvailableTenantUserDto>> GetAvailableTenantUsersAsync(Guid tenantId, string? search, CancellationToken cancellationToken);
    Task<EmployeeUserMappingDto?> GetByEmployeeAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeUserMappingDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeUserMappingRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid mappingId, byte[] rowVersion, CancellationToken cancellationToken);
}
