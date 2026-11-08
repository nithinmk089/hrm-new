namespace HRMS.Application.LeaveTypes;

/// <summary>Company leave-type configuration returned by the application layer.</summary>
public sealed record LeaveTypeDto(
    Guid LeaveTypeId,
    Guid CompanyId,
    string LeaveTypeCode,
    string LeaveTypeName,
    string? Description,
    bool IsPaid,
    bool RequiresApproval,
    decimal? AnnualEntitlementDays,
    bool AllowHalfDay,
    bool AllowCarryForward,
    decimal MaxCarryForwardDays,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);
