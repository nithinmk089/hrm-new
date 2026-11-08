namespace HRMS.Application.Designations;

/// <summary>Validates designation input and coordinates tenant-scoped persistence.</summary>
public sealed class DesignationService(IDesignationRepository repository) : IDesignationService
{
    public Task<IReadOnlyList<DesignationDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        return repository.GetAllAsync(tenantId, companyId, cancellationToken);
    }

    public Task<DesignationDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid designationId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(designationId, nameof(designationId));
        return repository.GetByIdAsync(tenantId, companyId, designationId, cancellationToken);
    }

    public async Task<DesignationDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateDesignationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        ArgumentNullException.ThrowIfNull(request);

        var code = Required(request.DesignationCode, nameof(request.DesignationCode), 30)
            .ToUpperInvariant();
        var name = Required(request.DesignationName, nameof(request.DesignationName), 150);
        var description = Optional(request.Description, 500);
        ValidateJobLevel(request.JobLevel);

        if (await repository.GetByCodeAsync(tenantId, companyId, code, cancellationToken) is not null)
            throw new InvalidOperationException($"Designation code '{code}' is already in use.");

        return await repository.CreateAsync(
            tenantId,
            companyId,
            new CreateDesignationRequest(code, name, description, request.JobLevel),
            cancellationToken);
    }

    public async Task<DesignationDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid designationId, UpdateDesignationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(designationId, nameof(designationId));
        ArgumentNullException.ThrowIfNull(request);

        if (request.RowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request.RowVersion));

        var name = Required(request.DesignationName, nameof(request.DesignationName), 150);
        var description = Optional(request.Description, 500);
        ValidateJobLevel(request.JobLevel);

        var existing = await repository.GetByIdAsync(
            tenantId, companyId, designationId, cancellationToken);
        if (existing is null)
            return null;

        var updated = await repository.UpdateAsync(
            tenantId,
            companyId,
            designationId,
            new UpdateDesignationRequest(name, description, request.JobLevel, request.RowVersion),
            cancellationToken);

        if (updated is null)
            throw new InvalidOperationException(
                "The designation was changed by another user. Reload the designation and retry your update.");

        return updated;
    }

    public Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid designationId, byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(designationId, nameof(designationId));
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
        return repository.DeactivateAsync(
            tenantId, companyId, designationId, rowVersion, cancellationToken);
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

    private static void ValidateJobLevel(short? jobLevel)
    {
        if (jobLevel is <= 0)
            throw new ArgumentException("JobLevel must be a positive number when supplied.", nameof(jobLevel));
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
            throw new ArgumentException($"Description cannot exceed {maxLength} characters.", nameof(value));
        return normalized;
    }
}
