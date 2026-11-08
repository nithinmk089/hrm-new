namespace HRMS.Application.Companies;

/// <summary>Company data returned by the application layer.</summary>
public sealed record CompanyDto(
    Guid CompanyId,
    string CompanyCode,
    string LegalName,
    string DisplayName,
    string? TaxRegistrationNumber,
    string CountryCode,
    string DefaultCurrencyCode,
    string TimeZoneId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
