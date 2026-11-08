namespace HRMS.Application.LeaveBalances;

public sealed record EmployeeLeaveBalanceDto(
    Guid EmployeeLeaveBalanceId,
    Guid CompanyId,
    Guid EmployeeId,
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    short LeaveYear,
    decimal OpeningDays,
    decimal AccruedDays,
    decimal CarriedForwardDays,
    decimal AdjustmentDays,
    decimal UsedDays,
    decimal ReservedDays,
    decimal AvailableDays,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion);

public sealed record ApplyLeaveBalanceTransactionRequest(
    Guid LeaveTypeId,
    short LeaveYear,
    string TransactionType,
    decimal DaysDelta,
    string? Notes);
