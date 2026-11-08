namespace HRMS.Application.Employees;

public sealed record EmployeeDto(
    Guid EmployeeId, Guid CompanyId, string EmployeeCode,
    string FirstName, string? MiddleName, string LastName, string? PreferredName,
    string? WorkEmail, DateTime HireDate, string EmploymentStatus,
    DateTime? TerminationDate, bool IsActive, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc, byte[] RowVersion);

public sealed record CreateEmployeeRequest(
    string EmployeeCode, string FirstName, string? MiddleName, string LastName,
    string? PreferredName, string? WorkEmail, DateTime HireDate);

public sealed record UpdateEmployeeRequest(
    string FirstName, string? MiddleName, string LastName,
    string? PreferredName, string? WorkEmail, DateTime HireDate, byte[] RowVersion);

public sealed record UpdateEmployeeStatusRequest(
    string EmploymentStatus, DateTime? TerminationDate, byte[] RowVersion);
