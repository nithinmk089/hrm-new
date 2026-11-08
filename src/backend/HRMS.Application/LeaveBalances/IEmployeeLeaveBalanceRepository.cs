namespace HRMS.Application.LeaveBalances;

public interface IEmployeeLeaveBalanceRepository
{
    Task<IReadOnlyList<EmployeeLeaveBalanceDto>> GetAllAsync(Guid tenantId, Guid companyId, Guid employeeId, short leaveYear, CancellationToken cancellationToken);
    Task<EmployeeLeaveBalanceDto> ApplyTransactionAsync(Guid tenantId, Guid companyId, Guid employeeId, Guid actorUserId, ApplyLeaveBalanceTransactionRequest request, CancellationToken cancellationToken);
}
