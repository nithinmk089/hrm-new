namespace HRMS.Application.LeaveRequests;

public interface ILeaveRequestLifecycleService
{
    Task<IReadOnlyList<EmployeeLeaveRequestItem>> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequestHistoryItem>> GetHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        CancellationToken cancellationToken = default);

    Task<LeaveRequestCancellationResult> CancelAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveRequestCancellationRequest request, CancellationToken cancellationToken = default);
}
