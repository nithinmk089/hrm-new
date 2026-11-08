namespace HRMS.Application.LeaveRequests;

public interface ILeaveRequestService
{
    Task<LeaveRequestSubmissionResult> SubmitAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorUserId,
        SubmitLeaveRequest request,
        CancellationToken cancellationToken = default);
}
