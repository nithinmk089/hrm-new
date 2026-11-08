using HRMS.API.Modules.Shared;
using HRMS.Application.Designations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/designations")]
[Authorize]
public sealed class DesignationsController(IDesignationService designationService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:designations.read")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null
            ? Forbid()
            : Ok(await designationService.GetAllAsync(tenantId.Value, companyId, cancellationToken));
    }

    [HttpGet("{designationId:guid}")]
    [Authorize(Policy = "Permission:designations.read")]
    public async Task<IActionResult> GetById(
        Guid companyId,
        Guid designationId,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var designation = await designationService.GetByIdAsync(
            tenantId.Value, companyId, designationId, cancellationToken);
        return designation is null ? NotFound() : Ok(designation);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:designations.create")]
    public async Task<IActionResult> Create(
        Guid companyId,
        [FromBody] CreateDesignationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var designation = await designationService.CreateAsync(
            tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { companyId, designationId = designation.DesignationId },
            designation);
    }

    [HttpPut("{designationId:guid}")]
    [Authorize(Policy = "Permission:designations.update")]
    public async Task<IActionResult> Update(
        Guid companyId,
        Guid designationId,
        [FromBody] UpdateDesignationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var designation = await designationService.UpdateAsync(
            tenantId.Value, companyId, designationId, request, cancellationToken);
        return designation is null ? NotFound() : Ok(designation);
    }

    [HttpPatch("{designationId:guid}/deactivate")]
    [Authorize(Policy = "Permission:designations.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId,
        Guid designationId,
        [FromBody] DeactivateDesignationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var deactivated = await designationService.DeactivateAsync(
            tenantId.Value, companyId, designationId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
}
