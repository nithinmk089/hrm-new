using HRMS.API.Modules.Shared;
using HRMS.Application.LeaveRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.LeaveManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/leave-requests")]
[Authorize]
public sealed class LeaveRequestsController(
    ILeaveRequestService service,
    ILeaveRequestLifecycleService lifecycleService) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "Permission:leave-requests.create")]
    public async Task<IActionResult> Submit(
        Guid companyId,
        [FromBody] SubmitLeaveRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.SubmitAsync(
            tenantId.Value, companyId, actorUserId.Value, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("mine")]
    [Authorize(Policy = "Permission:leave-requests.read")]
    public async Task<IActionResult> GetMine(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await lifecycleService.GetMineAsync(
            tenantId.Value, companyId, actorUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("mine/{leaveRequestId:guid}/history")]
    [Authorize(Policy = "Permission:leave-requests.read")]
    public async Task<IActionResult> GetHistory(
        Guid companyId, Guid leaveRequestId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await lifecycleService.GetHistoryAsync(
            tenantId.Value, companyId, actorUserId.Value, leaveRequestId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{leaveRequestId:guid}/cancel")]
    [Authorize(Policy = "Permission:leave-requests.cancel")]
    public async Task<IActionResult> Cancel(
        Guid companyId, Guid leaveRequestId,
        [FromBody] LeaveRequestCancellationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await lifecycleService.CancelAsync(
            tenantId.Value, companyId, actorUserId.Value, leaveRequestId, request, cancellationToken);
        return Ok(result);
    }
}
