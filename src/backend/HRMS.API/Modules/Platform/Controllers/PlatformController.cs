using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.Platform.Controllers;

[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public sealed class PlatformController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("version", Name = "GetApplicationVersion")]
    public IActionResult GetVersion() => Ok(new
    {
        application = "HRMS.API",
        version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
        environment = environment.EnvironmentName
    });
}
