using HRMS.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.Platform.Controllers;

[ApiController]
[Route("api/v1/health")]
[AllowAnonymous]
public sealed class HealthController(IDbConnectionFactory connectionFactory) : ControllerBase
{
    [HttpGet("live", Name = "GetLiveness")]
    public IActionResult Live() => Ok(new
    {
        status = "Healthy",
        service = "HRMS.API"
    });

    [HttpGet("ready", Name = "GetReadiness")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return Ok(new { status = "Ready", database = "Connected" });
    }
}
