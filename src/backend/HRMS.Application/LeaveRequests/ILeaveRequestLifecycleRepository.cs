namespace HRMS.Application.LeaveRequests;

public interface ILeaveRequestLifecycleRepository
{
    Task<IReadOnlyList<EmployeeLeaveRequestItem>> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeaveRequestHistoryItem>> GetHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        CancellationToken cancellationToken);

    Task<LeaveRequestCancellationResult> CancelAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        byte[] rowVersion, string? comments, CancellationToken cancellationToken);
}
