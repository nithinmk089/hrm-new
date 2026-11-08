namespace HRMS.Application.Designations;

/// <summary>Designation data returned by the application layer.</summary>
public sealed record DesignationDto(
    Guid DesignationId,
    Guid CompanyId,
    string DesignationCode,
    string DesignationName,
    string? Description,
    short? JobLevel,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
