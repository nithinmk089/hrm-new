namespace HRMS.Application.Holidays;

/// <summary>Validates holiday input and coordinates tenant-scoped persistence.</summary>
public sealed class HolidayService(IHolidayRepository repository) : IHolidayService
{
    public Task<IReadOnlyList<HolidayDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        return repository.GetAllAsync(tenantId, companyId, cancellationToken);
    }

    public Task<HolidayDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid holidayId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(holidayId, nameof(holidayId));
        return repository.GetByIdAsync(tenantId, companyId, holidayId, cancellationToken);
    }

    public async Task<HolidayDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        ArgumentNullException.ThrowIfNull(request);

        var name = Required(request.HolidayName, nameof(request.HolidayName), 150);
        var date = NormalizeDate(request.HolidayDate);
        var description = Optional(request.Description, 500);

        if (await repository.GetByDateAsync(tenantId, companyId, date, cancellationToken) is not null)
            throw new InvalidOperationException($"A holiday is already registered for {date:yyyy-MM-dd}.");

        return await repository.CreateAsync(
            tenantId, companyId,
            new CreateHolidayRequest(name, date, description, request.IsOptional),
            cancellationToken);
    }

    public async Task<HolidayDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid holidayId, UpdateHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(holidayId, nameof(holidayId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureRowVersion(request.RowVersion);

        var name = Required(request.HolidayName, nameof(request.HolidayName), 150);
        var date = NormalizeDate(request.HolidayDate);
        var description = Optional(request.Description, 500);

        var existing = await repository.GetByIdAsync(tenantId, companyId, holidayId, cancellationToken);
        if (existing is null) return null;

        var duplicate = await repository.GetByDateAsync(tenantId, companyId, date, cancellationToken);
        if (duplicate is not null && duplicate.HolidayId != holidayId)
            throw new InvalidOperationException($"A holiday is already registered for {date:yyyy-MM-dd}.");

        var updated = await repository.UpdateAsync(
            tenantId, companyId, holidayId,
            new UpdateHolidayRequest(name, date, description, request.IsOptional, request.RowVersion),
            cancellationToken);

        if (updated is null)
            throw new InvalidOperationException(
                "The holiday was changed by another user. Reload the holiday and retry your update.");

        return updated;
    }

    public Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid holidayId, byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        EnsureId(holidayId, nameof(holidayId));
        EnsureRowVersion(rowVersion);
        return repository.DeactivateAsync(tenantId, companyId, holidayId, rowVersion, cancellationToken);
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        if (value == default)
            throw new ArgumentException("HolidayDate is required.", nameof(value));
        return value.Date;
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
