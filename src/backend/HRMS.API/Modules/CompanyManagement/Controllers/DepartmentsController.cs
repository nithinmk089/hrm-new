using HRMS.API.Modules.Shared;
using HRMS.Application.Departments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/departments")]
[Authorize]
public sealed class DepartmentsController(IDepartmentService departmentService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:departments.read")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null
            ? Forbid()
            : Ok(await departmentService.GetAllAsync(tenantId.Value, companyId, cancellationToken));
    }

    [HttpGet("{departmentId:guid}")]
    [Authorize(Policy = "Permission:departments.read")]
    public async Task<IActionResult> GetById(
        Guid companyId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var department = await departmentService.GetByIdAsync(
            tenantId.Value, companyId, departmentId, cancellationToken);
        return department is null ? NotFound() : Ok(department);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:departments.create")]
    public async Task<IActionResult> Create(
        Guid companyId,
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var department = await departmentService.CreateAsync(
            tenantId.Value, companyId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { companyId, departmentId = department.DepartmentId },
            department);
    }

    [HttpPut("{departmentId:guid}")]
    [Authorize(Policy = "Permission:departments.update")]
    public async Task<IActionResult> Update(
        Guid companyId,
        Guid departmentId,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var department = await departmentService.UpdateAsync(
            tenantId.Value, companyId, departmentId, request, cancellationToken);
        return department is null ? NotFound() : Ok(department);
    }

    [HttpPatch("{departmentId:guid}/deactivate")]
    [Authorize(Policy = "Permission:departments.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId,
        Guid departmentId,
        [FromBody] DeactivateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var deactivated = await departmentService.DeactivateAsync(
            tenantId.Value, companyId, departmentId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
    [HttpPatch("{departmentId:guid}/activate")]
    [Authorize(Policy = "Permission:departments.update")]
    public async Task<IActionResult> Activate(
        Guid companyId,
        Guid departmentId,
        [FromBody] DeactivateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var activated = await departmentService.ActivateAsync(
            tenantId.Value, companyId, departmentId, request.RowVersion, cancellationToken);
        return activated ? NoContent() : Conflict();
    }

}
