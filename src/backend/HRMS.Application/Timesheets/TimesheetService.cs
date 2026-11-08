namespace HRMS.Application.Timesheets;

public sealed class TimesheetService(ITimesheetRepository repository, TimeProvider timeProvider)
    : ITimesheetService
{
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "ClientWork", "Internal", "Meeting", "Training", "Administration", "Other"
    };

    public Task<TimesheetDetailDto?> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
        CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        EnsureMonday(weekStartDate);
        return repository.GetMineAsync(tenantId, companyId, actorUserId, weekStartDate, cancellationToken);
    }

    public Task<TimesheetHistoryResponse> GetMineHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        return repository.GetMineHistoryAsync(tenantId, companyId, actorUserId, cancellationToken);
    }

    public Task<TimesheetCreateResult> CreateAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, TimesheetCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        ArgumentNullException.ThrowIfNull(request);
        EnsureMonday(request.WeekStartDate);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (request.WeekStartDate > today)
            throw new ArgumentException("A timesheet cannot be created for a future week.", nameof(request));
        return repository.CreateAsync(tenantId, companyId, actorUserId, request.WeekStartDate, cancellationToken);
    }

    public Task<SaveTimesheetEntriesResult> SaveEntriesAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        SaveTimesheetEntriesRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        EnsureId(timesheetId, nameof(timesheetId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureRowVersion(request.RowVersion);
        if (request.Entries is null || request.Entries.Count is 0 or > 200)
            throw new ArgumentException("Provide between 1 and 200 timesheet entries.", nameof(request));

        foreach (var entry in request.Entries)
        {
            if (!Categories.Contains(entry.WorkCategory))
                throw new ArgumentException("A timesheet entry has an unsupported work category.", nameof(request));
            if (string.IsNullOrWhiteSpace(entry.Description) || entry.Description.Trim().Length > 1000)
                throw new ArgumentException("Entry descriptions are required and cannot exceed 1000 characters.", nameof(request));
            if (entry.Hours <= 0 || entry.Hours > 24 || entry.Hours * 4 != decimal.Floor(entry.Hours * 4))
                throw new ArgumentException("Entry hours must be greater than zero, no more than 24, and in 0.25-hour increments.", nameof(request));
        }

        if (request.Entries.GroupBy(x => x.WorkDate).Any(g => g.Sum(x => x.Hours) > 24)
            || request.Entries.Sum(x => x.Hours) > 168)
            throw new ArgumentException("Daily hours cannot exceed 24 and weekly hours cannot exceed 168.", nameof(request));

        var normalized = request with
        {
            Entries = request.Entries.Select(x => x with
            {
                WorkCategory = x.WorkCategory.Trim(),
                Description = x.Description.Trim()
            }).ToArray()
        };
        return repository.SaveEntriesAsync(tenantId, companyId, actorUserId, timesheetId, normalized, cancellationToken);
    }

    public Task<TimesheetSubmitResult> SubmitAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetSubmitRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        EnsureId(timesheetId, nameof(timesheetId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureRowVersion(request.RowVersion);
        return repository.SubmitAsync(tenantId, companyId, actorUserId, timesheetId, request.RowVersion, cancellationToken);
    }

    public Task<IReadOnlyList<TimesheetApprovalQueueItem>> GetPendingApprovalsAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        return repository.GetPendingApprovalsAsync(tenantId, companyId, actorUserId, cancellationToken);
    }

    public Task<TimesheetDecisionResult> ApproveAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetDecisionRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(tenantId, companyId, actorUserId, timesheetId, request, "Approved", cancellationToken);

    public Task<TimesheetDecisionResult> RejectAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetDecisionRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(tenantId, companyId, actorUserId, timesheetId, request, "Rejected", cancellationToken);

    private Task<TimesheetDecisionResult> DecideAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        TimesheetDecisionRequest request, string decision, CancellationToken cancellationToken)
    {
        EnsureIds(tenantId, companyId, actorUserId);
        EnsureId(timesheetId, nameof(timesheetId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureRowVersion(request.RowVersion);
        var comments = request.Comments?.Trim();
        if (comments?.Length > 1000)
            throw new ArgumentException("Comments cannot exceed 1000 characters.", nameof(request));
        if (decision == "Rejected" && string.IsNullOrWhiteSpace(comments))
            throw new ArgumentException("Comments are required when rejecting a timesheet.", nameof(request));
        var normalized = request with { Comments = string.IsNullOrWhiteSpace(comments) ? null : comments };
        return repository.DecideAsync(tenantId, companyId, actorUserId, timesheetId, decision, normalized, cancellationToken);
    }

    private static void EnsureIds(Guid tenantId, Guid companyId, Guid actorUserId)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
        EnsureId(actorUserId, nameof(actorUserId));
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid identifier is required.", name);
    }

    private static void EnsureRowVersion(byte[]? rowVersion)
    {
        if (rowVersion is not { Length: 8 })
            throw new ArgumentException("A valid 8-byte RowVersion is required.");
    }

    private static void EnsureMonday(DateOnly date)
    {
        if (date == default || date.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("The timesheet week must start on a Monday.");
    }
}
