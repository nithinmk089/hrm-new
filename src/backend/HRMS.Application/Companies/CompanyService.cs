namespace HRMS.Application.Companies;

/// <summary>Validates company input and coordinates company persistence.</summary>
public sealed class CompanyService(ICompanyRepository repository) : ICompanyService
{
    public Task<IReadOnlyList<CompanyDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        EnsureValidId(companyId);
        return repository.GetByIdAsync(companyId, cancellationToken);
    }

    public async Task<CompanyDto> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyCode = Required(request.CompanyCode, nameof(request.CompanyCode), 30).ToUpperInvariant();
        var legalName = Required(request.LegalName, nameof(request.LegalName), 200);
        var displayName = Required(request.DisplayName, nameof(request.DisplayName), 150);
        var countryCode = Required(request.CountryCode, nameof(request.CountryCode), 2).ToUpperInvariant();
        var currencyCode = Required(request.DefaultCurrencyCode, nameof(request.DefaultCurrencyCode), 3).ToUpperInvariant();
        var timeZoneId = Required(request.TimeZoneId, nameof(request.TimeZoneId), 100);
        var taxNumber = Optional(request.TaxRegistrationNumber, 50);

        if (countryCode.Length != 2 || currencyCode.Length != 3)
        {
            throw new ArgumentException("CountryCode must contain 2 characters and DefaultCurrencyCode must contain 3 characters.");
        }

        var existing = await repository.GetByCodeAsync(companyCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Company code '{companyCode}' is already in use.");
        }

        return await repository.CreateAsync(
            new CreateCompanyRequest(companyCode, legalName, displayName, taxNumber, countryCode, currencyCode, timeZoneId),
            cancellationToken);
    }

    public async Task<CompanyDto?> UpdateAsync(
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureValidId(companyId);
        ArgumentNullException.ThrowIfNull(request);

        var legalName = Required(request.LegalName, nameof(request.LegalName), 200);
        var displayName = Required(request.DisplayName, nameof(request.DisplayName), 150);
        var countryCode = Required(request.CountryCode, nameof(request.CountryCode), 2).ToUpperInvariant();
        var currencyCode = Required(request.DefaultCurrencyCode, nameof(request.DefaultCurrencyCode), 3).ToUpperInvariant();
        var timeZoneId = Required(request.TimeZoneId, nameof(request.TimeZoneId), 100);
        var taxNumber = Optional(request.TaxRegistrationNumber, 50);

        if (countryCode.Length != 2 || currencyCode.Length != 3)
        {
            throw new ArgumentException("CountryCode must contain 2 characters and DefaultCurrencyCode must contain 3 characters.");
        }

        if (request.RowVersion is not { Length: 8 })
        {
            throw new ArgumentException("A valid 8-byte RowVersion is required for updates.", nameof(request.RowVersion));
        }

        return await repository.UpdateAsync(
            companyId,
            new UpdateCompanyRequest(legalName, displayName, taxNumber, countryCode, currencyCode, timeZoneId, request.RowVersion),
            cancellationToken);
    }

    public Task<bool> DeactivateAsync(
        Guid companyId,
        byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureValidId(companyId);

        if (rowVersion is not { Length: 8 })
        {
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(rowVersion));
        }

        return repository.DeactivateAsync(companyId, rowVersion, cancellationToken);
    }

    private static void EnsureValidId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A valid company ID is required.", nameof(id));
        }
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{name} cannot exceed {maxLength} characters.", name);
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
