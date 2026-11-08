namespace HRMS.Application.Timesheets;

public interface ITimesheetService
{
    Task<TimesheetDetailDto?> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
        CancellationToken cancellationToken = default);

    Task<TimesheetHistoryResponse> GetMineHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<TimesheetCreateResult> CreateAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, TimesheetCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<SaveTimesheetEntriesResult> SaveEntriesAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        SaveTimesheetEntriesRequest request, CancellationToken cancellationToken = default);

    Task<TimesheetSubmitResult> SubmitAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetSubmitRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimesheetApprovalQueueItem>> GetPendingApprovalsAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<TimesheetDecisionResult> ApproveAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetDecisionRequest request, CancellationToken cancellationToken = default);

    Task<TimesheetDecisionResult> RejectAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetDecisionRequest request, CancellationToken cancellationToken = default);
}
