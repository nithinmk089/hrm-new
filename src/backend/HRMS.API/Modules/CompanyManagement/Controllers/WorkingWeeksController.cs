using HRMS.API.Modules.Shared;
using HRMS.Application.WorkingWeeks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.CompanyManagement.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/working-week")]
[Authorize]
public sealed class WorkingWeeksController(IWorkingWeekService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:working-week.read")]
    public async Task<IActionResult> GetCompanyDefault(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null ? Forbid() :
            Ok(await service.GetAsync(tenantId.Value, companyId, null, cancellationToken));
    }

    [HttpPut]
    [Authorize(Policy = "Permission:working-week.update")]
    public async Task<IActionResult> SaveCompanyDefault(
        Guid companyId, [FromBody] SaveWorkingWeekRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null ? Forbid() :
            Ok(await service.SaveAsync(tenantId.Value, companyId, null, request, cancellationToken));
    }

    [HttpGet("~/api/v1/companies/{companyId:guid}/offices/{officeId:guid}/working-week")]
    [Authorize(Policy = "Permission:working-week.read")]
    public async Task<IActionResult> GetOfficeWeek(
        Guid companyId, Guid officeId, CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null ? Forbid() :
            Ok(await service.GetAsync(tenantId.Value, companyId, officeId, cancellationToken));
    }

    [HttpPut("~/api/v1/companies/{companyId:guid}/offices/{officeId:guid}/working-week")]
    [Authorize(Policy = "Permission:working-week.update")]
    public async Task<IActionResult> SaveOfficeWeek(
        Guid companyId, Guid officeId, [FromBody] SaveWorkingWeekRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = User.ReadTenantId();
        return tenantId is null ? Forbid() :
            Ok(await service.SaveAsync(tenantId.Value, companyId, officeId, request, cancellationToken));
    }
}
