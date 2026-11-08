using HRMS.API.Modules.Shared;
using HRMS.Application.Holidays;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/holidays")]
[Authorize]
public sealed class HolidaysController(IHolidayService holidayService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:holidays.read")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null
            ? Forbid()
            : Ok(await holidayService.GetAllAsync(tenantId.Value, companyId, cancellationToken));
    }

    [HttpGet("{holidayId:guid}")]
    [Authorize(Policy = "Permission:holidays.read")]
    public async Task<IActionResult> GetById(
        Guid companyId, Guid holidayId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var holiday = await holidayService.GetByIdAsync(
            tenantId.Value, companyId, holidayId, cancellationToken);
        return holiday is null ? NotFound() : Ok(holiday);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:holidays.create")]
    public async Task<IActionResult> Create(
        Guid companyId, [FromBody] CreateHolidayRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var holiday = await holidayService.CreateAsync(
            tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById), new { companyId, holidayId = holiday.HolidayId }, holiday);
    }

    [HttpPut("{holidayId:guid}")]
    [Authorize(Policy = "Permission:holidays.update")]
    public async Task<IActionResult> Update(
        Guid companyId, Guid holidayId, [FromBody] UpdateHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var holiday = await holidayService.UpdateAsync(
            tenantId.Value, companyId, holidayId, request, cancellationToken);
        return holiday is null ? NotFound() : Ok(holiday);
    }

    [HttpPatch("{holidayId:guid}/deactivate")]
    [Authorize(Policy = "Permission:holidays.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId, Guid holidayId, [FromBody] DeactivateHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var deactivated = await holidayService.DeactivateAsync(
            tenantId.Value, companyId, holidayId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
}
