using HRMS.API.Modules.Shared;
using HRMS.Application.LeaveBalances;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.LeaveManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/employees/{employeeId:guid}/leave-balances")]
[Authorize]
public sealed class EmployeeLeaveBalancesController(IEmployeeLeaveBalanceService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:leave-balances.read")]
    public async Task<IActionResult> GetAll(Guid companyId, Guid employeeId, [FromQuery] short year, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null ? Forbid() :
            Ok(await service.GetAllAsync(tenantId.Value, companyId, employeeId, year, cancellationToken));
    }

    [HttpPost("transactions")]
    [Authorize(Policy = "Permission:leave-balances.adjust")]
    public async Task<IActionResult> ApplyTransaction(
        Guid companyId, Guid employeeId, [FromBody] ApplyLeaveBalanceTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null) return Forbid();

        var balance = await service.ApplyTransactionAsync(
            tenantId.Value, companyId, employeeId, actorUserId.Value, request, cancellationToken);
        return Ok(balance);
    }
}
