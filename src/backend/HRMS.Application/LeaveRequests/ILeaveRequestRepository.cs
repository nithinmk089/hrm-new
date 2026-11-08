namespace HRMS.Application.LeaveRequests;

public interface ILeaveRequestRepository
{
    Task<LeaveRequestCalculationContext> GetCalculationContextAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveTypeId,
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);

    Task<LeaveRequestSubmissionResult> SubmitCalculatedAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorUserId,
        SubmitLeaveRequest request,
        decimal daysRequested,
        IReadOnlyList<LeaveYearReservationDto> yearAllocations,
        IReadOnlyList<DateOnly> excludedDates,
        CancellationToken cancellationToken);
}
