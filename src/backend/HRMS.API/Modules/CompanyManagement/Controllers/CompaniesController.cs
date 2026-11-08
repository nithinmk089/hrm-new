using HRMS.API.Modules.Shared;
using HRMS.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies")]
[Authorize]
public sealed class CompaniesController(ICompanyService companyService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:companies.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null
            ? Forbid()
            : Ok(await companyService.GetAllAsync(tenantId.Value, cancellationToken));
    }

    [HttpGet("{companyId:guid}")]
    [Authorize(Policy = "Permission:companies.read")]
    public async Task<IActionResult> GetById(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var company = await companyService.GetByIdAsync(
            tenantId.Value, companyId, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:companies.create")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var company = await companyService.CreateAsync(
            tenantId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { companyId = company.CompanyId }, company);
    }

    [HttpPut("{companyId:guid}")]
    [Authorize(Policy = "Permission:companies.update")]
    public async Task<IActionResult> Update(
        Guid companyId,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var company = await companyService.UpdateAsync(
            tenantId.Value, companyId, request, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPatch("{companyId:guid}/deactivate")]
    [Authorize(Policy = "Permission:companies.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId,
        [FromBody] DeactivateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        if (tenantId is null) return Forbid();

        var deactivated = await companyService.DeactivateAsync(
            tenantId.Value, companyId, request.RowVersion, cancellationToken);
        return deactivated ? NoContent() : Conflict();
    }
}
