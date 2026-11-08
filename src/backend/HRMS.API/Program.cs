using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using HRMS.API.Authorization;
using HRMS.API.ErrorHandling;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
using HRMS.Application.Companies;
using HRMS.Infrastructure;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtIssuer = jwtSection["Issuer"] ?? string.Empty;
var jwtAudience = jwtSection["Audience"] ?? string.Empty;
var jwtSigningKey = jwtSection["SigningKey"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = string.IsNullOrWhiteSpace(jwtSigningKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = "role"
        };
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var allowedOrigins = builder.Configuration
    .GetSection("Authentication:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("HrmsFrontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();
app.UseExceptionHandler();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    var appliedCount = await migrator.MigrateAsync();
    app.Logger.LogInformation(
        "Database migration command completed. {AppliedCount} migration(s) applied.", appliedCount);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("HrmsFrontend");
app.UseAuthentication();
app.UseAuthorization();

const string refreshCookieName = "hrms_refresh";
const string authCookiePath = "/api/v1/auth";

CookieOptions BuildRefreshCookieOptions(HttpContext context, DateTime expiresAtUtc) => new()
{
    HttpOnly = true,
    Secure = !app.Environment.IsDevelopment() || context.Request.IsHttps,
    SameSite = SameSiteMode.Strict,
    Path = authCookiePath,
    Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
    IsEssential = true
};

bool HasTrustedOrigin(HttpContext context)
{
    var origin = context.Request.Headers["Origin"].ToString();
    if (string.IsNullOrWhiteSpace(origin))
    {
        return true;
    }

    var configuredOrigins = builder.Configuration
        .GetSection("Authentication:AllowedOrigins")
        .Get<string[]>() ?? [];

    if (configuredOrigins.Length > 0)
    {
        return configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    return Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
           string.Equals(originUri.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(originUri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase);
}

IResult SetSessionCookieAndReturnLogin(HttpContext context, LoginSessionResult session)
{
    context.Response.Cookies.Append(
        refreshCookieName,
        session.RefreshToken,
        BuildRefreshCookieOptions(context, session.RefreshTokenExpiresAtUtc));

    return Results.Ok(session.Login);
}

app.MapGet("/api/v1/health/live", () => Results.Ok(new
{
    status = "Healthy",
    service = "HRMS.API"
}))
.WithName("GetLiveness")
.WithTags("Health");

app.MapGet("/api/v1/health/ready", async (
    IDbConnectionFactory connectionFactory,
    CancellationToken cancellationToken) =>
{
    await using var connection = connectionFactory.CreateConnection();
    await connection.OpenAsync(cancellationToken);
    return Results.Ok(new { status = "Ready", database = "Connected" });
})
.WithName("GetReadiness")
.WithTags("Health");

app.MapPost("/api/v1/auth/login", async (
    HttpContext context,
    LoginRequest request,
    IAuthenticationService authenticationService,
    CancellationToken cancellationToken) =>
{
    if (!HasTrustedOrigin(context))
    {
        return Results.Forbid();
    }

    var session = await authenticationService.LoginAsync(
        request, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);

    return session is null
        ? Results.Unauthorized()
        : SetSessionCookieAndReturnLogin(context, session);
})
.AllowAnonymous()
.WithName("Login")
.WithTags("Authentication");

app.MapPost("/api/v1/auth/refresh", async (
    HttpContext context,
    IRefreshTokenService refreshTokenService,
    CancellationToken cancellationToken) =>
{
    if (!HasTrustedOrigin(context))
    {
        return Results.Forbid();
    }

    if (!context.Request.Cookies.TryGetValue(refreshCookieName, out var refreshToken) ||
        string.IsNullOrWhiteSpace(refreshToken))
    {
        context.Response.Cookies.Delete(refreshCookieName, BuildRefreshCookieOptions(
            context, DateTime.UtcNow.AddDays(-1)));
        return Results.Unauthorized();
    }

    var result = await refreshTokenService.RefreshAsync(
        refreshToken, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);

    if (result.Status != RefreshTokenRotationStatus.Rotated || result.Session is null)
    {
        context.Response.Cookies.Delete(refreshCookieName, BuildRefreshCookieOptions(
            context, DateTime.UtcNow.AddDays(-1)));
        return Results.Unauthorized();
    }

    return SetSessionCookieAndReturnLogin(context, new LoginSessionResult(
        new LoginResult(
            result.Session.AccessToken.Value,
            "Bearer",
            result.Session.AccessToken.ExpiresAtUtc,
            result.Session.User.UserId,
            result.Session.User.DisplayName,
            result.Session.User.TenantId,
            result.Session.User.TenantCode,
            result.Session.User.TenantName),
        result.Session.RefreshToken,
        result.Session.RefreshTokenExpiresAtUtc));
})
.AllowAnonymous()
.WithName("RefreshAuthentication")
.WithTags("Authentication");

app.MapPost("/api/v1/auth/logout", async (
    HttpContext context,
    IRefreshTokenService refreshTokenService,
    CancellationToken cancellationToken) =>
{
    if (!HasTrustedOrigin(context))
    {
        return Results.Forbid();
    }

    if (context.Request.Cookies.TryGetValue(refreshCookieName, out var refreshToken) &&
        !string.IsNullOrWhiteSpace(refreshToken))
    {
        await refreshTokenService.RevokeAsync(
            refreshToken, context.Connection.RemoteIpAddress?.ToString(),
            "Logout", cancellationToken);
    }

    context.Response.Cookies.Delete(refreshCookieName, BuildRefreshCookieOptions(
        context, DateTime.UtcNow.AddDays(-1)));
    return Results.NoContent();
})
.AllowAnonymous()
.WithName("Logout")
.WithTags("Authentication");

app.MapGet("/api/v1/auth/me", (System.Security.Claims.ClaimsPrincipal principal) =>
{
    return Results.Ok(new
    {
        userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
        tenantId = principal.FindFirst("tenant_id")?.Value,
        tenantCode = principal.FindFirst("tenant_code")?.Value,
        membershipId = principal.FindFirst("membership_id")?.Value
    });
})
.RequireAuthorization()
.WithName("GetCurrentIdentity")
.WithTags("Authentication");

app.MapGet("/api/v1/version", () => Results.Ok(new
{
    application = "HRMS.API",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
    environment = app.Environment.EnvironmentName
}))
.WithName("GetApplicationVersion")
.WithTags("Platform");


app.MapGet("/api/v1/companies", async Task<IResult> (System.Security.Claims.ClaimsPrincipal principal, ICompanyService service, CancellationToken ct) =>
{
    var tenantId = ReadTenantId(principal);
    return tenantId is null ? Results.Forbid() : Results.Ok(await service.GetAllAsync(tenantId.Value, ct));
})
.RequireAuthorization("Permission:companies.read").WithName("GetCompanies").WithTags("Companies");

app.MapGet("/api/v1/companies/{companyId:guid}", async Task<IResult> (Guid companyId, System.Security.Claims.ClaimsPrincipal principal, ICompanyService service, CancellationToken ct) =>
{
    var tenantId = ReadTenantId(principal);
    if (tenantId is null) return Results.Forbid();
    var company = await service.GetByIdAsync(tenantId.Value, companyId, ct);
    return company is null ? Results.NotFound() : Results.Ok(company);
})
.RequireAuthorization("Permission:companies.read").WithName("GetCompanyById").WithTags("Companies");

app.MapPost("/api/v1/companies", async Task<IResult> (CreateCompanyRequest request, System.Security.Claims.ClaimsPrincipal principal, ICompanyService service, CancellationToken ct) =>
{
    var tenantId = ReadTenantId(principal);
    if (tenantId is null) return Results.Forbid();
    var company = await service.CreateAsync(tenantId.Value, request, ct);
    return Results.Created($"/api/v1/companies/{company.CompanyId}", company);
})
.RequireAuthorization("Permission:companies.create").WithName("CreateCompany").WithTags("Companies");

app.MapPut("/api/v1/companies/{companyId:guid}", async Task<IResult> (Guid companyId, UpdateCompanyRequest request, System.Security.Claims.ClaimsPrincipal principal, ICompanyService service, CancellationToken ct) =>
{
    var tenantId = ReadTenantId(principal);
    if (tenantId is null) return Results.Forbid();
    var company = await service.UpdateAsync(tenantId.Value, companyId, request, ct);
    return company is null ? Results.NotFound() : Results.Ok(company);
})
.RequireAuthorization("Permission:companies.update").WithName("UpdateCompany").WithTags("Companies");

app.MapPatch("/api/v1/companies/{companyId:guid}/deactivate", async Task<IResult> (Guid companyId, DeactivateCompanyRequest request, System.Security.Claims.ClaimsPrincipal principal, ICompanyService service, CancellationToken ct) =>
{
    var tenantId = ReadTenantId(principal);
    if (tenantId is null) return Results.Forbid();
    var result = await service.DeactivateAsync(tenantId.Value, companyId, request.RowVersion, ct);
    return result ? Results.NoContent() : Results.Conflict();
})
.RequireAuthorization("Permission:companies.deactivate").WithName("DeactivateCompany").WithTags("Companies");

Guid? ReadTenantId(System.Security.Claims.ClaimsPrincipal principal) =>
    Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId) && tenantId != Guid.Empty ? tenantId : null;


app.Run();

public partial class Program;
