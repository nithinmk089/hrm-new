using HRMS.API.Modules.Shared;
using HRMS.Application.LeaveRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.LeaveManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/leave-requests")]
[Authorize]
public sealed class LeaveRequestsController(ILeaveRequestService service) : ControllerBase
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
}
