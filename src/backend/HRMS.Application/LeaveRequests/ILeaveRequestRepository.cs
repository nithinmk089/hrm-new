namespace HRMS.Application.LeaveRequests;

public interface ILeaveRequestRepository
{
    Task<LeaveRequestSubmissionResult> SubmitAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorUserId,
        SubmitLeaveRequest request,
        CancellationToken cancellationToken);
}
