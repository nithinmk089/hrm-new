namespace HRMS.Application.Companies;
public sealed record CreateCompanyRequest(string CompanyCode, string LegalName, string DisplayName, string? TaxRegistrationNumber, string CountryCode, string DefaultCurrencyCode, string TimeZoneId);
public sealed record UpdateCompanyRequest(string LegalName, string DisplayName, string? TaxRegistrationNumber, string CountryCode, string DefaultCurrencyCode, string TimeZoneId, byte[] RowVersion);
public sealed record DeactivateCompanyRequest(byte[] RowVersion);