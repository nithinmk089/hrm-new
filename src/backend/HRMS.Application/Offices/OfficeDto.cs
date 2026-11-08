namespace HRMS.Application.Offices;

/// <summary>Office data returned by the application layer.</summary>
public sealed record OfficeDto(
    Guid OfficeId,
    Guid CompanyId,
    string OfficeCode,
    string OfficeName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateOrProvince,
    string? PostalCode,
    string CountryCode,
    string? TimeZoneId,
    bool IsHeadOffice,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
