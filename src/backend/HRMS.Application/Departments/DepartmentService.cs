namespace HRMS.Application.Departments;

/// <summary>Validates department input and coordinates tenant-scoped persistence.</summary>
public sealed class DepartmentService(IDepartmentRepository repository) : IDepartmentService
{
    public Task<IReadOnlyList<DepartmentDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        return repository.GetAllAsync(tenantId, companyId, cancellationToken);
    }

    public Task<DepartmentDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid departmentId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(departmentId, nameof(departmentId));
        return repository.GetByIdAsync(tenantId, companyId, departmentId, cancellationToken);
    }

    public async Task<DepartmentDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        ArgumentNullException.ThrowIfNull(request);

        var code = Required(request.DepartmentCode, nameof(request.DepartmentCode), 30).ToUpperInvariant();
        var name = Required(request.DepartmentName, nameof(request.DepartmentName), 150);
        var description = Optional(request.Description, 500);
        ValidateOptionalId(request.ParentDepartmentId, nameof(request.ParentDepartmentId));

        if (await repository.GetByCodeAsync(tenantId, companyId, code, cancellationToken) is not null)
            throw new InvalidOperationException($"Department code '{code}' is already in use.");

        await EnsureParentAsync(tenantId, companyId, request.ParentDepartmentId, cancellationToken);

        return await repository.CreateAsync(tenantId, companyId,
            new CreateDepartmentRequest(code, name, description, request.ParentDepartmentId),
            cancellationToken);
    }

    public async Task<DepartmentDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid departmentId, UpdateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(departmentId, nameof(departmentId));
        ArgumentNullException.ThrowIfNull(request);

        var name = Required(request.DepartmentName, nameof(request.DepartmentName), 150);
        var description = Optional(request.Description, 500);
        ValidateOptionalId(request.ParentDepartmentId, nameof(request.ParentDepartmentId));

        if (request.RowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request.RowVersion));

        if (request.ParentDepartmentId == departmentId)
            throw new ArgumentException("A department cannot be its own parent.", nameof(request.ParentDepartmentId));

        var existing = await repository.GetByIdAsync(
            tenantId, companyId, departmentId, cancellationToken);
        if (existing is null)
            return null;

        await EnsureParentAsync(tenantId, companyId, request.ParentDepartmentId, cancellationToken);

        var updated = await repository.UpdateAsync(tenantId, companyId, departmentId,
            new UpdateDepartmentRequest(name, description, request.ParentDepartmentId, request.RowVersion),
            cancellationToken);

        if (updated is null)
            throw new InvalidOperationException(
                "The department was changed by another user. Reload the department and retry your update.");

        return updated;
    }

    public Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid departmentId, byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(departmentId, nameof(departmentId));
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
        return repository.DeactivateAsync(tenantId, companyId, departmentId, rowVersion, cancellationToken);
    }

    public Task<bool> ActivateAsync(Guid tenantId, Guid companyId, Guid departmentId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(departmentId, nameof(departmentId));
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
        return repository.ActivateAsync(tenantId, companyId, departmentId, rowVersion, cancellationToken);
    }

    private async Task EnsureParentAsync(
        Guid tenantId, Guid companyId, Guid? parentDepartmentId, CancellationToken cancellationToken)
    {
        if (parentDepartmentId is null)
            return;

        if (!await repository.ParentExistsAsync(
            tenantId, companyId, parentDepartmentId.Value, cancellationToken))
        {
            throw new ArgumentException(
                "ParentDepartmentId must identify an active department in the same company.",
                nameof(parentDepartmentId));
        }
    }

    private static void EnsureScope(Guid tenantId, Guid companyId)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid identifier is required.", name);
    }

    private static void ValidateOptionalId(Guid? id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid identifier is required when a value is supplied.", name);
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{name} is required.", name);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{name} cannot exceed {maxLength} characters.", name);
        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
            throw new ArgumentException($"Description cannot exceed {maxLength} characters.");
        return normalized;
    }
}
