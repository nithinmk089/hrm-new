using HRMS.API.Modules.Shared;
using HRMS.Application.Timesheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.TimesheetManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/timesheets")]
[Authorize]
public sealed class TimesheetsController(ITimesheetService service) : ControllerBase
{
    [HttpGet("mine")]
    [Authorize(Policy = "Permission:timesheets.read")]
    public async Task<IActionResult> GetMine(
        Guid companyId, [FromQuery] DateOnly weekStartDate, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.GetMineAsync(
            tenantId.Value, companyId, actorUserId.Value, weekStartDate, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("mine/history")]
    [Authorize(Policy = "Permission:timesheets.read")]
    public async Task<IActionResult> GetMineHistory(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.GetMineHistoryAsync(
            tenantId.Value, companyId, actorUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:timesheets.manage")]
    public async Task<IActionResult> Create(
        Guid companyId, [FromBody] TimesheetCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.CreateAsync(
            tenantId.Value, companyId, actorUserId.Value, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{timesheetId:guid}/entries")]
    [Authorize(Policy = "Permission:timesheets.manage")]
    public async Task<IActionResult> SaveEntries(
        Guid companyId, Guid timesheetId,
        [FromBody] SaveTimesheetEntriesRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.SaveEntriesAsync(
            tenantId.Value, companyId, actorUserId.Value, timesheetId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{timesheetId:guid}/submit")]
    [Authorize(Policy = "Permission:timesheets.submit")]
    public async Task<IActionResult> Submit(
        Guid companyId, Guid timesheetId,
        [FromBody] TimesheetSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var actorUserId = User.ReadUserId();
        if (tenantId is null || actorUserId is null)
            return Forbid();

        var result = await service.SubmitAsync(
            tenantId.Value, companyId, actorUserId.Value, timesheetId, request, cancellationToken);
        return Ok(result);
    }
}
