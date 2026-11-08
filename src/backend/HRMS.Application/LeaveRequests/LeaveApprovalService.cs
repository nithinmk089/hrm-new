namespace HRMS.Application.LeaveRequests;

public sealed class LeaveApprovalService(ILeaveApprovalRepository repository) : ILeaveApprovalService
{
    public Task<IReadOnlyList<LeaveApprovalQueueItem>> GetPendingAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
        EnsureId(actorUserId, nameof(actorUserId));
        return repository.GetPendingAsync(tenantId, companyId, actorUserId, cancellationToken);
    }

    public Task<LeaveApprovalDecisionResult> ApproveAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveApprovalDecisionRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(tenantId, companyId, actorUserId, leaveRequestId, request, "Approved", cancellationToken);

    public Task<LeaveApprovalDecisionResult> RejectAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveApprovalDecisionRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(tenantId, companyId, actorUserId, leaveRequestId, request, "Rejected", cancellationToken);

    private Task<LeaveApprovalDecisionResult> DecideAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveApprovalDecisionRequest request, string action, CancellationToken cancellationToken)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
        EnsureId(actorUserId, nameof(actorUserId));
        EnsureId(leaveRequestId, nameof(leaveRequestId));
        ArgumentNullException.ThrowIfNull(request);

        if (request.RowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request));

        var comments = request.Comments?.Trim();
        if (comments?.Length > 1000)
            throw new ArgumentException("Comments cannot exceed 1000 characters.", nameof(request));

        if (action == "Rejected" && string.IsNullOrWhiteSpace(comments))
            throw new ArgumentException("Comments are required when rejecting a leave request.", nameof(request));

        return repository.DecideAsync(
            tenantId, companyId, actorUserId, leaveRequestId, action,
            request.RowVersion, string.IsNullOrWhiteSpace(comments) ? null : comments,
            cancellationToken);
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid identifier is required.", name);
    }
}
