namespace HRMS.Application.LeaveRequests;

public sealed class LeaveRequestService(ILeaveRequestRepository repository) : ILeaveRequestService
{
    public async Task<LeaveRequestSubmissionResult> SubmitAsync(
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
        var context = await repository.GetCalculationContextAsync(
            tenantId, companyId, actorUserId, normalized.LeaveTypeId,
            normalized.FromDate, normalized.ToDate, cancellationToken);

        var calculation = LeaveDayCalculator.Calculate(new LeaveDayCalculationRequest(
            normalized.FromDate,
            normalized.ToDate,
            normalized.IsHalfDay,
            context.AllowHalfDay,
            context.CompanyWorkingWeek,
            context.OfficeWorkingWeeks,
            context.OfficeAssignments,
            context.Holidays));

        if (calculation.ChargeableDays <= 0)
            throw new ArgumentException("The selected date range contains no chargeable working days.", nameof(request));

        var allocations = calculation.ChargeableDates
            .GroupBy(date => date.Year)
            .OrderBy(group => group.Key)
            .Select(group => new LeaveYearReservationDto(checked((short)group.Key),
                normalized.IsHalfDay ? 0.5m : group.Count()))
            .ToArray();

        return await repository.SubmitCalculatedAsync(
            tenantId, companyId, actorUserId, normalized, calculation.ChargeableDays,
            allocations, calculation.ExcludedDates, cancellationToken);
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid identifier is required.", name);
    }
}
