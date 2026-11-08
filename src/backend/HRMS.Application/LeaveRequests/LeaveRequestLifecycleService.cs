namespace HRMS.Application.LeaveRequests;

public sealed class LeaveRequestLifecycleService(ILeaveRequestLifecycleRepository repository)
    : ILeaveRequestLifecycleService
{
    public Task<IReadOnlyList<EmployeeLeaveRequestItem>> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        return repository.GetMineAsync(tenantId, companyId, actorUserId, cancellationToken);
    }

    public Task<IReadOnlyList<LeaveRequestHistoryItem>> GetHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        if (leaveRequestId == Guid.Empty)
            throw new ArgumentException("A valid leave request identifier is required.", nameof(leaveRequestId));
        return repository.GetHistoryAsync(tenantId, companyId, actorUserId, leaveRequestId, cancellationToken);
    }

    public Task<LeaveRequestCancellationResult> CancelAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        LeaveRequestCancellationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        if (leaveRequestId == Guid.Empty)
            throw new ArgumentException("A valid leave request identifier is required.", nameof(leaveRequestId));
        ArgumentNullException.ThrowIfNull(request);
        if (request.RowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request));
        var comments = request.Comments?.Trim();
        if (comments?.Length > 1000)
            throw new ArgumentException("Comments cannot exceed 1000 characters.", nameof(request));
        return repository.CancelAsync(tenantId, companyId, actorUserId, leaveRequestId,
            request.RowVersion, string.IsNullOrWhiteSpace(comments) ? null : comments, cancellationToken);
    }

    private static void EnsureIds(Guid tenantId, Guid companyId, Guid actorUserId)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Valid tenant, company and authenticated user identifiers are required.");
    }
}
