namespace HRMS.Application.LeaveRequests;

public interface ILeaveApprovalRepository
{
    Task<IReadOnlyList<LeaveApprovalQueueItem>> GetPendingAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken);

    Task<LeaveApprovalDecisionResult> DecideAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        string action, byte[] rowVersion, string? comments, CancellationToken cancellationToken);
}
