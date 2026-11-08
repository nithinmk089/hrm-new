namespace HRMS.Application.Timesheets;

public interface ITimesheetRepository
{
    Task<TimesheetDetailDto?> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
        CancellationToken cancellationToken);

    Task<TimesheetHistoryResponse> GetMineHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken);

    Task<TimesheetCreateResult> CreateAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
        CancellationToken cancellationToken);

    Task<SaveTimesheetEntriesResult> SaveEntriesAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        SaveTimesheetEntriesRequest request, CancellationToken cancellationToken);

    Task<TimesheetSubmitResult> SubmitAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        byte[] rowVersion, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimesheetApprovalQueueItem>> GetPendingApprovalsAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken);

    Task<TimesheetDecisionResult> DecideAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        string decision, TimesheetDecisionRequest request, CancellationToken cancellationToken);
}
