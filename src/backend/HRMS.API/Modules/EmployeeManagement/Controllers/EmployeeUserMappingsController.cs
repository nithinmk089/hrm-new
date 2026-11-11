using HRMS.API.Modules.Shared;
using HRMS.Application.EmployeeUserMappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.EmployeeManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/employee-user-mappings")]
[Authorize]
public sealed class EmployeeUserMappingsController(IEmployeeUserMappingService service) : ControllerBase
{
    [HttpGet("available-users")]
    [Authorize(Policy = "Permission:employee-mappings.read")]
    public async Task<IActionResult> GetAvailableUsers(Guid companyId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var users = await service.GetAvailableTenantUsersAsync(tenantId.Value, companyId, search, cancellationToken);
        return Ok(users);
    }

    [HttpGet("mine")]
    [Authorize(Policy = "Permission:leave-balances.read")]
    public async Task<IActionResult> GetMine(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        var userId = User.ReadUserId();
        if (tenantId is null || userId is null) return Forbid();
        var mapping = await service.GetMineAsync(tenantId.Value, companyId, userId.Value, cancellationToken);
        return mapping is null ? NotFound() : Ok(mapping);
    }

    [HttpGet("by-employee/{employeeId:guid}")]
    [Authorize(Policy = "Permission:employee-mappings.read")]
    public async Task<IActionResult> GetByEmployee(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var mapping = await service.GetByEmployeeAsync(tenantId.Value, companyId, employeeId, cancellationToken);
        return mapping is null ? NotFound() : Ok(mapping);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:employee-mappings.manage")]
    public async Task<IActionResult> Create(Guid companyId, [FromBody] CreateEmployeeUserMappingRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var mapping = await service.CreateAsync(tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetByEmployee), new { companyId, employeeId = mapping.EmployeeId }, mapping);
    }

    [HttpPatch("{mappingId:guid}/deactivate")]
    [Authorize(Policy = "Permission:employee-mappings.manage")]
    public async Task<IActionResult> Deactivate(Guid companyId, Guid mappingId, [FromBody] DeactivateEmployeeUserMappingRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var deactivated = await service.DeactivateAsync(tenantId.Value, companyId, mappingId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
}
