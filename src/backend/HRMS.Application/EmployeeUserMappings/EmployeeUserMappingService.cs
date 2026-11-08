namespace HRMS.Application.EmployeeUserMappings;

public sealed class EmployeeUserMappingService(IEmployeeUserMappingRepository repository) : IEmployeeUserMappingService
{
    public Task<EmployeeUserMappingDto?> GetByEmployeeAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(employeeId, nameof(employeeId));
        return repository.GetByEmployeeAsync(tenantId, companyId, employeeId, cancellationToken);
    }

    public Task<EmployeeUserMappingDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeUserMappingRequest request, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        ArgumentNullException.ThrowIfNull(request);
        EnsureId(request.EmployeeId, nameof(request.EmployeeId));
        EnsureId(request.UserId, nameof(request.UserId));
        return repository.CreateAsync(tenantId, companyId, request, cancellationToken);
    }

    public Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid mappingId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(mappingId, nameof(mappingId));
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
        return repository.DeactivateAsync(tenantId, companyId, mappingId, rowVersion, cancellationToken);
    }

    private static void EnsureScope(Guid tenantId, Guid companyId)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("A valid identifier is required.", name);
    }
}
