using HRMS.API.Modules.Shared;
using HRMS.Application.Offices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/offices")]
[Authorize]
public sealed class OfficesController(IOfficeService officeService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:offices.read")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null
            ? Forbid()
            : Ok(await officeService.GetAllAsync(tenantId.Value, companyId, cancellationToken));
    }

    [HttpGet("{officeId:guid}")]
    [Authorize(Policy = "Permission:offices.read")]
    public async Task<IActionResult> GetById(
        Guid companyId,
        Guid officeId,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var office = await officeService.GetByIdAsync(
            tenantId.Value, companyId, officeId, cancellationToken);
        return office is null ? NotFound() : Ok(office);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:offices.create")]
    public async Task<IActionResult> Create(
        Guid companyId,
        [FromBody] CreateOfficeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var office = await officeService.CreateAsync(
            tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { companyId, officeId = office.OfficeId },
            office);
    }

    [HttpPut("{officeId:guid}")]
    [Authorize(Policy = "Permission:offices.update")]
    public async Task<IActionResult> Update(
        Guid companyId,
        Guid officeId,
        [FromBody] UpdateOfficeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var office = await officeService.UpdateAsync(
            tenantId.Value, companyId, officeId, request, cancellationToken);
        return office is null ? NotFound() : Ok(office);
    }

    [HttpPatch("{officeId:guid}/deactivate")]
    [Authorize(Policy = "Permission:offices.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId,
        Guid officeId,
        [FromBody] DeactivateOfficeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var deactivated = await officeService.DeactivateAsync(
            tenantId.Value, companyId, officeId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
}
