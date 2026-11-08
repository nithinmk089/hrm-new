namespace HRMS.Application.Offices;

public sealed record CreateOfficeRequest(
    string OfficeCode,
    string OfficeName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateOrProvince,
    string? PostalCode,
    string CountryCode,
    string? TimeZoneId,
    bool IsHeadOffice);

public sealed record UpdateOfficeRequest(
    string OfficeName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateOrProvince,
    string? PostalCode,
    string CountryCode,
    string? TimeZoneId,
    bool IsHeadOffice,
    byte[] RowVersion);

public sealed record DeactivateOfficeRequest(byte[] RowVersion);
