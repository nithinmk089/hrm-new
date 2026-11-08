using HRMS.API.Modules.Shared;
using HRMS.Application.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.EmployeeManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/employees")]
[Authorize]
public sealed class EmployeesController(IEmployeeService employeeService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:employees.read")]
    public async Task<IActionResult> GetAll(Guid companyId, [FromQuery] string? search, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null ? Forbid() : Ok(await employeeService.GetAllAsync(tenantId.Value, companyId, search, status, cancellationToken));
    }

    [HttpGet("{employeeId:guid}")]
    [Authorize(Policy = "Permission:employees.read")]
    public async Task<IActionResult> GetById(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var employee = await employeeService.GetByIdAsync(tenantId.Value, companyId, employeeId, cancellationToken);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:employees.create")]
    public async Task<IActionResult> Create(Guid companyId, [FromBody] CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var employee = await employeeService.CreateAsync(tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { companyId, employeeId = employee.EmployeeId }, employee);
    }

    [HttpPut("{employeeId:guid}")]
    [Authorize(Policy = "Permission:employees.update")]
    public async Task<IActionResult> Update(Guid companyId, Guid employeeId, [FromBody] UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var employee = await employeeService.UpdateAsync(tenantId.Value, companyId, employeeId, request, cancellationToken);
        return employee is null ? Conflict() : Ok(employee);
    }

    [HttpPatch("{employeeId:guid}/status")]
    [Authorize(Policy = "Permission:employees.update")]
    public async Task<IActionResult> UpdateStatus(Guid companyId, Guid employeeId, [FromBody] UpdateEmployeeStatusRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();
        var updated = await employeeService.UpdateStatusAsync(tenantId.Value, companyId, employeeId, request, cancellationToken);
        return updated ? NoContent() : Conflict();
    }
}
