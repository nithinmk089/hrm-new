namespace HRMS.Application.Departments;

/// <summary>Department data returned by the application layer.</summary>
public sealed record DepartmentDto(
    Guid DepartmentId,
    Guid CompanyId,
    string DepartmentCode,
    string DepartmentName,
    string? Description,
    Guid? ParentDepartmentId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
