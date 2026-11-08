namespace HRMS.Application.LeaveTypes;

/// <summary>Validates leave-type configuration and coordinates tenant-scoped persistence.</summary>
public sealed class LeaveTypeService(ILeaveTypeRepository repository) : ILeaveTypeService
{
    public Task<IReadOnlyList<LeaveTypeDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        return repository.GetAllAsync(tenantId, companyId, cancellationToken);
    }

    public Task<LeaveTypeDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid leaveTypeId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(leaveTypeId, nameof(leaveTypeId));
        return repository.GetByIdAsync(tenantId, companyId, leaveTypeId, cancellationToken);
    }

    public async Task<LeaveTypeDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateLeaveTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        ArgumentNullException.ThrowIfNull(request);

        var code = Required(request.LeaveTypeCode, nameof(request.LeaveTypeCode), 30).ToUpperInvariant();
        var name = Required(request.LeaveTypeName, nameof(request.LeaveTypeName), 100);
        var description = Optional(request.Description, 500);
        ValidateEntitlements(request.AnnualEntitlementDays, request.AllowCarryForward, request.MaxCarryForwardDays);

        if (await repository.GetByCodeAsync(tenantId, companyId, code, cancellationToken) is not null)
            throw new InvalidOperationException($"Leave type code '{code}' is already in use.");

        return await repository.CreateAsync(
            tenantId, companyId,
            new CreateLeaveTypeRequest(code, name, description, request.IsPaid,
                request.RequiresApproval, request.AnnualEntitlementDays, request.AllowHalfDay,
                request.AllowCarryForward, request.MaxCarryForwardDays),
            cancellationToken);
    }

    public async Task<LeaveTypeDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid leaveTypeId, UpdateLeaveTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(leaveTypeId, nameof(leaveTypeId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureRowVersion(request.RowVersion);

        var name = Required(request.LeaveTypeName, nameof(request.LeaveTypeName), 100);
        var description = Optional(request.Description, 500);
        ValidateEntitlements(request.AnnualEntitlementDays, request.AllowCarryForward, request.MaxCarryForwardDays);

        var existing = await repository.GetByIdAsync(tenantId, companyId, leaveTypeId, cancellationToken);
        if (existing is null) return null;

        var updated = await repository.UpdateAsync(
            tenantId, companyId, leaveTypeId,
            new UpdateLeaveTypeRequest(name, description, request.IsPaid, request.RequiresApproval,
                request.AnnualEntitlementDays, request.AllowHalfDay, request.AllowCarryForward,
                request.MaxCarryForwardDays, request.RowVersion),
            cancellationToken);

        if (updated is null)
            throw new InvalidOperationException(
                "The leave type was changed by another user. Reload it and retry your update.");

        return updated;
    }

    public Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid leaveTypeId, byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(leaveTypeId, nameof(leaveTypeId));
        EnsureRowVersion(rowVersion);
        return repository.DeactivateAsync(tenantId, companyId, leaveTypeId, rowVersion, cancellationToken);
    }

    private static void ValidateEntitlements(decimal? annualDays, bool allowCarryForward, decimal carryForwardDays)
    {
        if (annualDays is < 0 or > 366)
            throw new ArgumentException("AnnualEntitlementDays must be between 0 and 366 when supplied.", nameof(annualDays));
        if (carryForwardDays < 0 || carryForwardDays > 366)
            throw new ArgumentException("MaxCarryForwardDays must be between 0 and 366.", nameof(carryForwardDays));
        if (!allowCarryForward && carryForwardDays != 0)
            throw new ArgumentException("MaxCarryForwardDays must be zero when carry-forward is disabled.", nameof(carryForwardDays));
        if (allowCarryForward && carryForwardDays <= 0)
            throw new ArgumentException("A positive MaxCarryForwardDays is required when carry-forward is enabled.", nameof(carryForwardDays));
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

    private static void EnsureRowVersion(byte[]? rowVersion)
    {
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
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
