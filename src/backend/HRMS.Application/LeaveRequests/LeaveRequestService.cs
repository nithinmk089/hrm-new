namespace HRMS.Application.LeaveRequests;

public sealed class LeaveRequestService(ILeaveRequestRepository repository) : ILeaveRequestService
{
    public Task<LeaveRequestSubmissionResult> SubmitAsync(
        Guid tenantId, Guid companyId, Guid actorUserId,
        SubmitLeaveRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
        EnsureId(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureId(request.LeaveTypeId, nameof(request.LeaveTypeId));

        if (request.ToDate < request.FromDate)
            throw new ArgumentException("ToDate cannot be earlier than FromDate.", nameof(request));
        if (request.ToDate.DayNumber - request.FromDate.DayNumber + 1 > 366)
            throw new ArgumentException("A leave request cannot exceed 366 calendar days.", nameof(request));
        if (request.IsHalfDay && request.FromDate != request.ToDate)
            throw new ArgumentException("A half-day request must cover exactly one date.", nameof(request));
        var halfDayPeriod = string.IsNullOrWhiteSpace(request.HalfDayPeriod)
            ? null
            : request.HalfDayPeriod.Trim().ToUpperInvariant();
        if (request.IsHalfDay && halfDayPeriod is not ("AM" or "PM"))
            throw new ArgumentException("HalfDayPeriod must be AM or PM for a half-day request.", nameof(request));
        if (!request.IsHalfDay && halfDayPeriod is not null)
            throw new ArgumentException("HalfDayPeriod must be empty for a full-day request.", nameof(request));
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required.", nameof(request));
        if (reason.Length > 1000)
            throw new ArgumentException("Reason cannot exceed 1000 characters.", nameof(request));

        var normalized = request with { HalfDayPeriod = halfDayPeriod, Reason = reason };
        return repository.SubmitAsync(tenantId, companyId, actorUserId, normalized, cancellationToken);
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid identifier is required.", name);
    }
}
