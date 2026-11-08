using System.Text;
using HRMS.API.Authorization;
using HRMS.API.ErrorHandling;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
using HRMS.Infrastructure;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    const string bearerScheme = "Bearer";

    options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT access token. Swagger UI will add the Bearer prefix automatically."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(bearerScheme, document)] = []
    });
});

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
            NameClaimType = "name",
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

app.UseMiddleware<ApiErrorLoggingMiddleware>();
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

if (args.Contains("--seed-development", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Development seed data can only be created when ASP.NET Core is running in the Development environment.");
    }

    if (!app.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
    {
        throw new InvalidOperationException(
            "Development seed is disabled. Set DevelopmentSeed:Enabled=true in local development configuration.");
    }

    await using var seedScope = app.Services.CreateAsyncScope();
    var seedService = seedScope.ServiceProvider.GetRequiredService<IDevelopmentSeedService>();
    var seedResult = await seedService.SeedAsync();

    app.Logger.LogInformation(
        "Development seed completed. Tenant {TenantCode}; user {UserEmail}; company {CompanyCode}; " +
        "administrator role {RoleId}; {PermissionCount} active permission(s) granted; user created: {UserCreated}.",
        seedResult.TenantCode,
        seedResult.UserEmail,
        seedResult.CompanyCode,
        seedResult.RoleId,
        seedResult.GrantedPermissionCount,
        seedResult.UserCreated);

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

app.MapControllers();

app.Run();

public partial class Program;
