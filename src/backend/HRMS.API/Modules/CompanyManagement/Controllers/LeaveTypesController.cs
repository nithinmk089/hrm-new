using HRMS.API.Modules.Shared;
using HRMS.Application.LeaveTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/leave-types")]
[Authorize]
public sealed class LeaveTypesController(ILeaveTypeService leaveTypeService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:leave-types.read")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null
            ? Forbid()
            : Ok(await leaveTypeService.GetAllAsync(tenantId.Value, companyId, cancellationToken));
    }

    [HttpGet("{leaveTypeId:guid}")]
    [Authorize(Policy = "Permission:leave-types.read")]
    public async Task<IActionResult> GetById(Guid companyId, Guid leaveTypeId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var leaveType = await leaveTypeService.GetByIdAsync(
            tenantId.Value, companyId, leaveTypeId, cancellationToken);
        return leaveType is null ? NotFound() : Ok(leaveType);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:leave-types.create")]
    public async Task<IActionResult> Create(
        Guid companyId, [FromBody] CreateLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var leaveType = await leaveTypeService.CreateAsync(
            tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById), new { companyId, leaveTypeId = leaveType.LeaveTypeId }, leaveType);
    }

    [HttpPut("{leaveTypeId:guid}")]
    [Authorize(Policy = "Permission:leave-types.update")]
    public async Task<IActionResult> Update(
        Guid companyId, Guid leaveTypeId, [FromBody] UpdateLeaveTypeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var leaveType = await leaveTypeService.UpdateAsync(
            tenantId.Value, companyId, leaveTypeId, request, cancellationToken);
        return leaveType is null ? NotFound() : Ok(leaveType);
    }

    [HttpPatch("{leaveTypeId:guid}/deactivate")]
    [Authorize(Policy = "Permission:leave-types.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId, Guid leaveTypeId, [FromBody] DeactivateLeaveTypeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var deactivated = await leaveTypeService.DeactivateAsync(
            tenantId.Value, companyId, leaveTypeId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
}
