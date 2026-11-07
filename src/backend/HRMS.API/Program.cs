using System.Reflection;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

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

    return Results.Ok(new
    {
        status = "Ready",
        database = "Connected"
    });
})
.WithName("GetReadiness")
.WithTags("Health");

app.MapGet("/api/v1/version", () => Results.Ok(new
{
    application = "HRMS.API",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
    environment = app.Environment.EnvironmentName
}))
.WithName("GetApplicationVersion")
.WithTags("Platform");

app.Run();

public partial class Program;
