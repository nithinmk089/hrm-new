namespace HRMS.Application.Departments;

public sealed record CreateDepartmentRequest(
    string DepartmentCode,
    string DepartmentName,
    string? Description,
    Guid? ParentDepartmentId);

public sealed record UpdateDepartmentRequest(
    string DepartmentName,
    string? Description,
    Guid? ParentDepartmentId,
    byte[] RowVersion);

public sealed record DeactivateDepartmentRequest(byte[] RowVersion);
