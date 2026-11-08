namespace HRMS.Application.LeaveTypes;

public sealed record CreateLeaveTypeRequest(
    string LeaveTypeCode,
    string LeaveTypeName,
    string? Description,
    bool IsPaid,
    bool RequiresApproval,
    decimal? AnnualEntitlementDays,
    bool AllowHalfDay,
    bool AllowCarryForward,
    decimal MaxCarryForwardDays);

public sealed record UpdateLeaveTypeRequest(
    string LeaveTypeName,
    string? Description,
    bool IsPaid,
    bool RequiresApproval,
    decimal? AnnualEntitlementDays,
    bool AllowHalfDay,
    bool AllowCarryForward,
    decimal MaxCarryForwardDays,
    byte[] RowVersion);

public sealed record DeactivateLeaveTypeRequest(byte[] RowVersion);
