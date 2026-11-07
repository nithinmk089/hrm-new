using System.Reflection;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Infrastructure;
using HRMS.API.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

// API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Exception handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

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
