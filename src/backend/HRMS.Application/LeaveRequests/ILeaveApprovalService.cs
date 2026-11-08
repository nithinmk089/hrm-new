namespace HRMS.Application.LeaveRequests;

public interface ILeaveApprovalService
{
    Task<IReadOnlyList<LeaveApprovalQueueItem>> GetPendingAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<LeaveApprovalDecisionResult> ApproveAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveApprovalDecisionRequest request, CancellationToken cancellationToken = default);

    Task<LeaveApprovalDecisionResult> RejectAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveApprovalDecisionRequest request, CancellationToken cancellationToken = default);
}
