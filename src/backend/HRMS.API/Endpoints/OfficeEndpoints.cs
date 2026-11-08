using HRMS.Application.Offices;
using Microsoft.AspNetCore.Routing;

namespace HRMS.API.Endpoints;

/// <summary>Maps company-scoped office management endpoints.</summary>
public static class OfficeEndpoints
{
    public static IEndpointRouteBuilder MapOfficeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/companies/{companyId:guid}/offices")
            .WithTags("Offices");

        group.MapGet("", async Task<IResult> (
            Guid companyId, System.Security.Claims.ClaimsPrincipal principal,
            IOfficeService service, CancellationToken cancellationToken) =>
        {
            var tenantId = ReadTenantId(principal);
            return tenantId is null
                ? Results.Forbid()
                : Results.Ok(await service.GetAllAsync(tenantId.Value, companyId, cancellationToken));
        })
        .RequireAuthorization("Permission:offices.read")
        .WithName("GetOffices");

        group.MapGet("/{officeId:guid}", async Task<IResult> (
            Guid companyId, Guid officeId, System.Security.Claims.ClaimsPrincipal principal,
            IOfficeService service, CancellationToken cancellationToken) =>
        {
            var tenantId = ReadTenantId(principal);
            if (tenantId is null) return Results.Forbid();

            var office = await service.GetByIdAsync(
                tenantId.Value, companyId, officeId, cancellationToken);
            return office is null ? Results.NotFound() : Results.Ok(office);
        })
        .RequireAuthorization("Permission:offices.read")
        .WithName("GetOfficeById");

        group.MapPost("", async Task<IResult> (
            Guid companyId, CreateOfficeRequest request,
            System.Security.Claims.ClaimsPrincipal principal,
            IOfficeService service, CancellationToken cancellationToken) =>
        {
            var tenantId = ReadTenantId(principal);
            if (tenantId is null) return Results.Forbid();

            var office = await service.CreateAsync(
                tenantId.Value, companyId, request, cancellationToken);
            return Results.Created(
                $"/api/v1/companies/{companyId}/offices/{office.OfficeId}", office);
        })
        .RequireAuthorization("Permission:offices.create")
        .WithName("CreateOffice");

        group.MapPut("/{officeId:guid}", async Task<IResult> (
            Guid companyId, Guid officeId, UpdateOfficeRequest request,
            System.Security.Claims.ClaimsPrincipal principal,
            IOfficeService service, CancellationToken cancellationToken) =>
        {
            var tenantId = ReadTenantId(principal);
            if (tenantId is null) return Results.Forbid();

            var office = await service.UpdateAsync(
                tenantId.Value, companyId, officeId, request, cancellationToken);
            return office is null ? Results.NotFound() : Results.Ok(office);
        })
        .RequireAuthorization("Permission:offices.update")
        .WithName("UpdateOffice");

        group.MapPatch("/{officeId:guid}/deactivate", async Task<IResult> (
            Guid companyId, Guid officeId, DeactivateOfficeRequest request,
            System.Security.Claims.ClaimsPrincipal principal,
            IOfficeService service, CancellationToken cancellationToken) =>
        {
            var tenantId = ReadTenantId(principal);
            if (tenantId is null) return Results.Forbid();

            var deactivated = await service.DeactivateAsync(
                tenantId.Value, companyId, officeId, request.RowVersion, cancellationToken);
            return deactivated ? Results.NoContent() : Results.Conflict();
        })
        .RequireAuthorization("Permission:offices.deactivate")
        .WithName("DeactivateOffice");

        return endpoints;
    }

    private static Guid? ReadTenantId(System.Security.Claims.ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId) &&
        tenantId != Guid.Empty
            ? tenantId
            : null;
}
