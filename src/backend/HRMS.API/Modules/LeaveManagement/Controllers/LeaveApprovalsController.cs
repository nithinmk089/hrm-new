using HRMS.API.Modules.Shared;
using HRMS.Application.LeaveRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.LeaveManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/leave-requests")]
[Authorize]
public sealed class LeaveApprovalsController(ILeaveApprovalService service) : ControllerBase
{
    [HttpGet("pending-approvals")]
    [Authorize(Policy = "Permission:leave-requests.read")]
    public async Task<IActionResult> GetPending(
        Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.GetPendingAsync(
            tenantId.Value, companyId, actorUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{leaveRequestId:guid}/approve")]
    [Authorize(Policy = "Permission:leave-requests.approve")]
    public async Task<IActionResult> Approve(
        Guid companyId, Guid leaveRequestId,
        [FromBody] LeaveApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.ApproveAsync(
            tenantId.Value, companyId, actorUserId.Value, leaveRequestId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{leaveRequestId:guid}/reject")]
    [Authorize(Policy = "Permission:leave-requests.reject")]
    public async Task<IActionResult> Reject(
        Guid companyId, Guid leaveRequestId,
        [FromBody] LeaveApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.RejectAsync(
            tenantId.Value, companyId, actorUserId.Value, leaveRequestId, request, cancellationToken);
        return Ok(result);
    }
}
