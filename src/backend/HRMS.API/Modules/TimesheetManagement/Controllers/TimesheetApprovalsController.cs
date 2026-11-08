using HRMS.API.Modules.Shared;
using HRMS.Application.Timesheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.TimesheetManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/timesheets")]
[Authorize]
public sealed class TimesheetApprovalsController(ITimesheetService service) : ControllerBase
{
    [HttpGet("pending-approvals")]
    [Authorize(Policy = "Permission:timesheets.read")]
    public async Task<IActionResult> GetPending(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.GetPendingApprovalsAsync(
            tenantId.Value, companyId, actorUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{timesheetId:guid}/approve")]
    [Authorize(Policy = "Permission:timesheets.approve")]
    public async Task<IActionResult> Approve(
        Guid companyId, Guid timesheetId,
        [FromBody] TimesheetDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.ApproveAsync(
            tenantId.Value, companyId, actorUserId.Value, timesheetId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{timesheetId:guid}/reject")]
    [Authorize(Policy = "Permission:timesheets.reject")]
    public async Task<IActionResult> Reject(
        Guid companyId, Guid timesheetId,
        [FromBody] TimesheetDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.RejectAsync(
            tenantId.Value, companyId, actorUserId.Value, timesheetId, request, cancellationToken);
        return Ok(result);
    }
}
