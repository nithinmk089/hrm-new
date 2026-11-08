namespace HRMS.Application.Offices;

/// <summary>Validates office input and coordinates tenant-scoped persistence.</summary>
public sealed class OfficeService(IOfficeRepository repository) : IOfficeService
{
    public Task<IReadOnlyList<OfficeDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        return repository.GetAllAsync(tenantId, companyId, cancellationToken);
    }

    public Task<OfficeDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid officeId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(officeId, nameof(officeId));
        return repository.GetByIdAsync(tenantId, companyId, officeId, cancellationToken);
    }

    public async Task<OfficeDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateOfficeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        ArgumentNullException.ThrowIfNull(request);

        var code = Required(request.OfficeCode, nameof(request.OfficeCode), 30).ToUpperInvariant();
        var name = Required(request.OfficeName, nameof(request.OfficeName), 150);
        var normalized = new CreateOfficeRequest(
            code,
            name,
            Optional(request.AddressLine1, 200, nameof(request.AddressLine1)),
            Optional(request.AddressLine2, 200, nameof(request.AddressLine2)),
            Optional(request.City, 100, nameof(request.City)),
            Optional(request.StateOrProvince, 100, nameof(request.StateOrProvince)),
            Optional(request.PostalCode, 20, nameof(request.PostalCode)),
            CountryCode(request.CountryCode),
            Optional(request.TimeZoneId, 100, nameof(request.TimeZoneId)),
            request.IsHeadOffice);

        if (await repository.GetByCodeAsync(tenantId, companyId, code, cancellationToken) is not null)
            throw new InvalidOperationException($"Office code '{code}' is already in use.");

        return await repository.CreateAsync(tenantId, companyId, normalized, cancellationToken);
    }

    public async Task<OfficeDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid officeId, UpdateOfficeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(officeId, nameof(officeId));
        ArgumentNullException.ThrowIfNull(request);

        if (request.RowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request.RowVersion));

        var existing = await repository.GetByIdAsync(
            tenantId, companyId, officeId, cancellationToken);
        if (existing is null)
            return null;

        var normalized = new UpdateOfficeRequest(
            Required(request.OfficeName, nameof(request.OfficeName), 150),
            Optional(request.AddressLine1, 200, nameof(request.AddressLine1)),
            Optional(request.AddressLine2, 200, nameof(request.AddressLine2)),
            Optional(request.City, 100, nameof(request.City)),
            Optional(request.StateOrProvince, 100, nameof(request.StateOrProvince)),
            Optional(request.PostalCode, 20, nameof(request.PostalCode)),
            CountryCode(request.CountryCode),
            Optional(request.TimeZoneId, 100, nameof(request.TimeZoneId)),
            request.IsHeadOffice,
            request.RowVersion);

        var updated = await repository.UpdateAsync(
            tenantId, companyId, officeId, normalized, cancellationToken);

        if (updated is null)
            throw new InvalidOperationException(
                "The office was changed by another user. Reload the office and retry your update.");

        return updated;
    }

    public Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid officeId, byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(officeId, nameof(officeId));
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
        return repository.DeactivateAsync(tenantId, companyId, officeId, rowVersion, cancellationToken);
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

    private static string CountryCode(string? value)
    {
        var normalized = Required(value, "CountryCode", 2).ToUpperInvariant();
        if (normalized.Length != 2 || normalized.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("CountryCode must be a valid two-letter ISO country code.", nameof(value));
        return normalized;
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

    private static string? Optional(string? value, int maxLength, string name)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
            throw new ArgumentException($"{name} cannot exceed {maxLength} characters.", name);
        return normalized;
    }
}
