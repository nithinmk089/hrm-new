namespace HRMS.Application.EmployeeUserMappings;

public sealed record EmployeeUserMappingDto(
    Guid EmployeeUserMappingId,
    Guid CompanyId,
    Guid EmployeeId,
    Guid UserId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
public sealed record CreateEmployeeUserMappingRequest(Guid EmployeeId, Guid UserId);
public sealed record DeactivateEmployeeUserMappingRequest(byte[] RowVersion);
