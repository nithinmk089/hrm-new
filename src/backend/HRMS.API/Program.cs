using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using HRMS.API.ErrorHandling;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
using HRMS.Infrastructure;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Exception handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// JWT bearer authentication. A missing signing key prevents token issuance
// and causes supplied bearer tokens to fail validation; it does not block health checks.
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

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

// Apply database migrations only when explicitly requested.
// This keeps normal API startup free from schema-changing operations.
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    var appliedCount = await migrator.MigrateAsync();

    app.Logger.LogInformation(
        "Database migration command completed. {AppliedCount} migration(s) applied.",
        appliedCount);

    return;
}

// Swagger UI is enabled only in the Development environment.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Liveness
app.MapGet("/api/v1/health/live", () => Results.Ok(new
{
    status = "Healthy",
    service = "HRMS.API"
}))
.WithName("GetLiveness")
.WithTags("Health");

// Readiness
app.MapGet("/api/v1/health/ready", async (
    IDbConnectionFactory connectionFactory,
    CancellationToken cancellationToken) =>
{
    await using var connection = connectionFactory.CreateConnection();
    await connection.OpenAsync(cancellationToken);

    return Results.Ok(new
    {
        status = "Ready",
        database = "Connected"
    });
})
.WithName("GetReadiness")
.WithTags("Health");

// Login
app.MapPost("/api/v1/auth/login", async (
    LoginRequest request,
    IAuthenticationService authenticationService,
    CancellationToken cancellationToken) =>
{
    var result = await authenticationService.LoginAsync(request, cancellationToken);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
})
.AllowAnonymous()
.WithName("Login")
.WithTags("Authentication");

// Current authenticated identity. Permission and scope checks will be added
// in the authorization increment; this endpoint only verifies bearer-token validation.
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

// Version
app.MapGet("/api/v1/version", () => Results.Ok(new
{
    application = "HRMS.API",
    version = Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "unknown",
    environment = app.Environment.EnvironmentName
}))
.WithName("GetApplicationVersion")
.WithTags("Platform");

app.Run();

public partial class Program;
